using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.SampleRequests.Commands.RetryWorkflowNotificationJob;
using HRM.Application.Features.PLM.SampleRequests.Commands.RestoreWorkflowSummary;
using HRM.Domain.Entities.InternalMailSchema;
using Microsoft.EntityFrameworkCore.Metadata;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetWorkflowNotificationJobs;
using HRM.Application.Tests.Features.InternalMail.GroupStorage;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

/// <summary>Real row locks and tenant boundaries on a fresh isolated cluster; never touches API settings/database.</summary>
public sealed class WorkflowNotificationJobPostgresTests(IsolatedGroupStoragePostgres postgres)
    : IClassFixture<IsolatedGroupStoragePostgres>
{
    [PostgresGroupStorageFact]
    public async Task RetryKeepsOriginalRowAndPayloadHonorsWorkerLockAndRejectsOtherCompany()
    {
        await using var connection = await postgres.OpenAsync();
        await using (var schema = new NpgsqlCommand("""
            CREATE SCHEMA notification;
            CREATE SCHEMA "SampleRequests";
            CREATE TABLE notification.outbox_messages (id bigint PRIMARY KEY, type text NOT NULL, payload_json jsonb NOT NULL,
                created_at timestamp NOT NULL, processed_at timestamp NULL, attempts integer NOT NULL, error text NULL);
            CREATE TABLE "SampleRequests"."SampleRequests" ("SampleRequestId" uuid PRIMARY KEY, "CompanyId" uuid NOT NULL, "ExternalId" text NOT NULL, "IsActive" boolean NOT NULL DEFAULT TRUE);
            CREATE TABLE "SampleRequests"."SampleRequestWorkflowEvents" (
                "Id" uuid PRIMARY KEY, "CompanyId" uuid NOT NULL, "SampleRequestId" uuid NOT NULL, "EventType" text NOT NULL,
                "OccurredAt" timestamp NULL, "RecordedAt" timestamp NOT NULL, "RecordedByEmployeeId" uuid NULL,
                "Note" text NULL, "FormulaId" uuid NULL, "SampleRequestSampleTrialId" uuid NULL,
                "ProductPricingVersionId" uuid NULL, "QuotationLineId" uuid NULL, "PriceReference" text NULL,
                "RelatedEventId" uuid NULL, "ConversationId" uuid NULL, "MessageId" uuid NULL, "NotificationId" uuid NULL);
            """, connection)) await schema.ExecuteNonQueryAsync();
        var company = Guid.NewGuid();
        var sample = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var payload = System.Text.Json.JsonSerializer.Serialize(new { CompanyId = company, EventId = eventId, Message = "not exposed" });
        await using (var seed = new NpgsqlCommand("""
            INSERT INTO "SampleRequests"."SampleRequests" ("SampleRequestId","CompanyId","ExternalId") VALUES (@sample,@company,'TP_TEST');
            INSERT INTO "SampleRequests"."SampleRequestWorkflowEvents" ("Id","CompanyId","SampleRequestId","EventType","RecordedAt")
                VALUES (@event,@company,@sample,'LabStarted',localtimestamp);
            INSERT INTO notification.outbox_messages VALUES (123,'SampleRequestWorkflowNotification',@payload::jsonb,localtimestamp,NULL,5,'private legacy error');
            """, connection))
        {
            seed.Parameters.AddWithValue("sample", sample);
            seed.Parameters.AddWithValue("company", company);
            seed.Parameters.AddWithValue("event", eventId);
            seed.Parameters.AddWithValue("payload", payload);
            await seed.ExecuteNonQueryAsync();
        }
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        var user = new User(company);
        var permissions = new CurrentUserPermissionService(user);
        var list = await new GetWorkflowNotificationJobsQueryHandler(db, user, permissions).Handle(new("TP_TEST", "Failed"), default);
        Assert.NotNull(list);
        Assert.Single(list.Items);
        Assert.True(list.Items[0].CanRetry);
        Assert.Equal("WorkflowNotification.Unknown", list.Items[0].ErrorCode);
        var outsider = new User(Guid.NewGuid());
        var otherPermissions = new CurrentUserPermissionService(outsider);
        var otherList = await new GetWorkflowNotificationJobsQueryHandler(db, outsider, otherPermissions).Handle(new(Status: "All"), default);
        Assert.NotNull(otherList);
        Assert.Empty(otherList.Items);
        Assert.Equal("Unavailable", (await Handler(db, outsider).Handle(new(123), default)).Outcome);

        // A worker transaction owns the same outbox lock on an independent connection.
        await using (var worker = await postgres.OpenAsync())
        await using (var transaction = await worker.BeginTransactionAsync())
        {
            await using var hold = new NpgsqlCommand("SELECT id FROM notification.outbox_messages WHERE id=123 FOR UPDATE", worker, transaction);
            await hold.ExecuteScalarAsync();
            Assert.Equal("Unavailable", (await Handler(db, user).Handle(new(123), default)).Outcome);
            await transaction.RollbackAsync();
        }
        Assert.Equal("Queued", (await Handler(db, user).Handle(new(123), default)).Outcome);
        Assert.Equal("AlreadyQueued", (await Handler(db, user).Handle(new(123), default)).Outcome);
        await using (var verify = new NpgsqlCommand("SELECT count(*),max(attempts),max(payload_json::text) FROM notification.outbox_messages", connection))
        await using (var reader = await verify.ExecuteReaderAsync())
        {
            Assert.True(await reader.ReadAsync());
            Assert.Equal(1L, reader.GetInt64(0));
            Assert.Equal(0, reader.GetInt32(1));
            using var json = System.Text.Json.JsonDocument.Parse(reader.GetString(2));
            Assert.Equal(eventId, json.RootElement.GetProperty("EventId").GetGuid());
            Assert.Equal("not exposed", json.RootElement.GetProperty("Message").GetString());
        }
        db.ChangeTracker.Clear();
        await using (var done = new NpgsqlCommand("UPDATE notification.outbox_messages SET processed_at=localtimestamp,attempts=5", connection))
            await done.ExecuteNonQueryAsync();
        Assert.Equal("Completed", (await Handler(db, user).Handle(new(123), default)).Outcome);
        await using (var legacy = new NpgsqlCommand("UPDATE notification.outbox_messages SET error='No handler for type SampleRequestWorkflowNotification'", connection))
            await legacy.ExecuteNonQueryAsync();
        var attention = await new GetWorkflowNotificationJobsQueryHandler(db, user, permissions).Handle(new(Status: "Attention"), default);
        Assert.NotNull(attention);
        var review = Assert.Single(attention.Items);
        Assert.Equal("NeedsReview", review.Status);
        Assert.Equal("WorkflowNotification.LegacyHandlerMissing", review.ErrorCode);
        Assert.True(review.CanRetry);
        var completed = await new GetWorkflowNotificationJobsQueryHandler(db, user, permissions).Handle(new(Status: "Completed"), default);
        Assert.NotNull(completed);
        Assert.Empty(completed.Items);

        // Recovery uses a real isolated schema, with no notification/read-state tables available to write accidentally.
        await using (var setup = new NpgsqlCommand("""
            CREATE SCHEMA "InternalMail";
            CREATE SCHEMA hr;
            CREATE TABLE hr."Employees" ("EmployeeID" uuid PRIMARY KEY,"CompanyId" uuid NOT NULL);
            ALTER TABLE "SampleRequests"."SampleRequests" ADD COLUMN "CreatedDate" timestamp NOT NULL DEFAULT '2026-11-02 08:00:00';
            """, connection)) await setup.ExecuteNonQueryAsync();
        foreach (var type in new[] { typeof(InternalConversation), typeof(InternalMessage) })
        {
            var entity = db.Model.FindEntityType(type)!;
            var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
            // Identifiers/types come only from the trusted EF model in this disposable test database.
            var columns = entity.GetProperties().Select(p => $"\"{p.GetColumnName(table)}\" {p.GetRelationalTypeMapping().StoreType} NULL");
            var key = entity.FindPrimaryKey()!.Properties.Single().GetColumnName(table);
            await using var create = new NpgsqlCommand($"CREATE TABLE \"{table.Schema}\".\"{table.Name}\" ({string.Join(',', columns)}, PRIMARY KEY (\"{key}\"))", connection);
            await create.ExecuteNonQueryAsync();
        }
        await using (var defaults = new NpgsqlCommand("""
            ALTER TABLE "InternalMail"."InternalMessages" ALTER COLUMN "IsDeleted" SET DEFAULT FALSE,
              ALTER COLUMN "IsEdited" SET DEFAULT FALSE, ALTER COLUMN "IsUrgent" SET DEFAULT FALSE,
              ALTER COLUMN "MessageType" SET DEFAULT 0;
            """, connection)) await defaults.ExecuteNonQueryAsync();
        var root = Guid.NewGuid();
        db.ChangeTracker.Clear();
        Assert.Equal("Queued", (await Handler(db, user).Handle(new(123), default)).Outcome);
        Assert.Equal("AlreadyQueued", (await Handler(db, user).Handle(new(123), default)).Outcome);
        await using (var reset = new NpgsqlCommand("UPDATE notification.outbox_messages SET processed_at=localtimestamp,error='No handler for type SampleRequestWorkflowNotification'", connection))
            await reset.ExecuteNonQueryAsync();
        db.ChangeTracker.Clear();
        var latest = Guid.NewGuid();
        const string summary = "Original saved summary";
        await using (var seedSummary = new NpgsqlCommand("""
            INSERT INTO hr."Employees" VALUES (@sender,@company);
            INSERT INTO "InternalMail"."InternalConversations"
              ("InternalConversationId","CompanyId","Subject","RelatedId","RelatedType","CreatedBy","CreatedAt","LastMessageAt","LastMessageId","IsActive")
              VALUES (@root,@company,'Summary test',@sample,5,@sender,'2026-11-02 08:00:00','2026-11-03 09:00:00',@latest,TRUE);
            UPDATE "SampleRequests"."SampleRequestWorkflowEvents" SET "EventType"='SampleRequestWorkflowCreated',"RecordedByEmployeeId"=@sender;
            UPDATE notification.outbox_messages SET payload_json=@payload::jsonb;
            """, connection))
        {
            seedSummary.Parameters.AddWithValue("sender", user.EmployeeId!.Value);
            seedSummary.Parameters.AddWithValue("company", company);
            seedSummary.Parameters.AddWithValue("sample", sample);
            seedSummary.Parameters.AddWithValue("root", root);
            seedSummary.Parameters.AddWithValue("latest", latest);
            seedSummary.Parameters.AddWithValue("payload", System.Text.Json.JsonSerializer.Serialize(new { CompanyId = company, EventId = eventId, IsInitialMessage = true, Message = summary }));
            await seedSummary.ExecuteNonQueryAsync();
        }
        db.ChangeTracker.Clear();
        RestoreWorkflowSummaryCommandHandler Restore(User current) => new(db, current, new CurrentUserPermissionService(current), NullLogger<RestoreWorkflowSummaryCommandHandler>.Instance, null!);
        Assert.Equal("Unavailable", (await Restore(outsider).Handle(new(123), default)).Outcome);
        var eligible = await new GetWorkflowNotificationJobsQueryHandler(db, user, permissions).Handle(new(Status: "Attention"), default);
        Assert.True(Assert.Single(eligible!.Items).CanRestoreSummary);
        var duplicateId = Guid.NewGuid();
        db.InternalMessages.Add(new InternalMessage { InternalMessageId = duplicateId, InternalConversationId = root,
            SenderEmployeeId = user.EmployeeId.Value, Body = summary, SentAt = new(2026, 11, 2, 8, 0, 0) });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        Assert.Equal("RecoveryConflict", (await Restore(user).Handle(new(123), default)).Outcome);
        await db.InternalMessages.Where(x => x.InternalMessageId == duplicateId).ExecuteDeleteAsync();
        db.ChangeTracker.Clear();
        Assert.Equal("Restored", (await Restore(user).Handle(new(123), default)).Outcome);
        db.ChangeTracker.Clear();
        Assert.Equal("AlreadyRestored", (await Restore(user).Handle(new(123), default)).Outcome);
        var restored = Assert.Single(await db.InternalMessages.AsNoTracking().ToListAsync());
        Assert.Equal(eventId, restored.InternalMessageId);
        Assert.Equal(summary, restored.Body);
        Assert.Equal(new DateTime(2026, 11, 2, 8, 0, 0), restored.SentAt);
        var conversation = await db.InternalConversations.AsNoTracking().SingleAsync();
        Assert.Equal(latest, conversation.LastMessageId);
        Assert.Equal(new DateTime(2026, 11, 3, 9, 0, 0), conversation.LastMessageAt);
        var recovered = await new GetWorkflowNotificationJobsQueryHandler(db, user, permissions).Handle(new(Status: "All"), default);
        Assert.True(Assert.Single(recovered!.Items).HasMessage);
        Assert.False(Assert.Single(recovered.Items).CanRestoreSummary);

        // An inactive historical root is not permission to recreate a removed group.
        await db.InternalMessages.Where(x => x.InternalMessageId == eventId).ExecuteDeleteAsync();
        await db.SampleRequestWorkflowEvents.ExecuteUpdateAsync(s => s
            .SetProperty(x => x.MessageId, (Guid?)null).SetProperty(x => x.ConversationId, (Guid?)null));
        await db.InternalConversations.ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false));
        db.ChangeTracker.Clear();
        Assert.Equal("RecoveryConflict", (await Restore(user).Handle(new(123), default)).Outcome);

        // With no root at all, delegate to the same trusted message writer used for creation.
        await db.InternalConversations.ExecuteDeleteAsync();
        db.ChangeTracker.Clear();
        var dispatcher = System.Reflection.DispatchProxy.Create<MediatR.ISender, RestoreSender>();
        var capture = (RestoreSender)(object)dispatcher;
        capture.Result = HRM.Application.Commons.Models.OperationResult<HRM.Application.Features.InternalMail.Dtos.SendInternalMessageResultDto>.Fail("Unavailable recipient");
        var handler = new RestoreWorkflowSummaryCommandHandler(db, user, permissions,
            NullLogger<RestoreWorkflowSummaryCommandHandler>.Instance, dispatcher);
        Assert.Equal("RecoveryConversationUnavailable", (await handler.Handle(new(123), default)).Outcome);
        Assert.True(capture.Command!.RestoreInitialSummaryHistory);
        Assert.True(capture.Command.DeferNotification);
        Assert.True(capture.Command.IsInitialMessage);
        Assert.Equal(eventId, capture.Command.WorkflowEventId);
        Assert.Equal(summary, capture.Command.Message);
        Assert.Empty(await db.InternalMessages.AsNoTracking().ToListAsync());
        Assert.Empty(await db.InternalConversations.AsNoTracking().ToListAsync());
        db.ChangeTracker.Clear();
        var originalJob = await db.OutboxMessages.AsNoTracking().SingleAsync();
        Assert.NotNull(originalJob.ProcessedAt);
        Assert.Equal("No handler for type SampleRequestWorkflowNotification", originalJob.Error);
        var recreatedRoot = Guid.NewGuid();
        capture.Callback = async () =>
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "InternalMail"."InternalConversations"
                  ("InternalConversationId","CompanyId","Subject","RelatedId","RelatedType","CreatedBy","CreatedAt","LastMessageAt","LastMessageId","IsActive")
                  VALUES ({recreatedRoot},{company},'Restored',{sample},5,{user.EmployeeId.Value},'2026-11-02 08:00:00','2026-11-02 08:00:00',{eventId},TRUE)
                """);
            db.InternalMessages.Add(new InternalMessage { InternalMessageId = eventId,
                InternalConversationId = recreatedRoot, SenderEmployeeId = user.EmployeeId.Value,
                Body = summary, SentAt = new(2026, 11, 2, 8, 0, 0) });
            await db.SaveChangesAsync();
            return HRM.Application.Commons.Models.OperationResult<HRM.Application.Features.InternalMail.Dtos.SendInternalMessageResultDto>.Ok(
                new HRM.Application.Features.InternalMail.Dtos.SendInternalMessageResultDto { ConversationId = recreatedRoot, MessageId = eventId });
        };
        Assert.Equal("Restored", (await handler.Handle(new(123), default)).Outcome);
        db.ChangeTracker.Clear();
        capture.Callback = () => throw new InvalidOperationException("Repeated restore must not invoke writer");
        Assert.Equal("AlreadyRestored", (await handler.Handle(new(123), default)).Outcome);
        Assert.Single(await db.InternalMessages.AsNoTracking().ToListAsync());
        Assert.Equal(recreatedRoot, (await db.SampleRequestWorkflowEvents.AsNoTracking().SingleAsync()).ConversationId);
    }

    public class RestoreSender : System.Reflection.DispatchProxy
    {
        public HRM.Application.Features.PLM.SampleRequests.Commands.SendSampleRequestMessage.SendSampleRequestMessageCommand? Command;
        public HRM.Application.Commons.Models.OperationResult<HRM.Application.Features.InternalMail.Dtos.SendInternalMessageResultDto> Result = null!;
        public Func<Task<HRM.Application.Commons.Models.OperationResult<HRM.Application.Features.InternalMail.Dtos.SendInternalMessageResultDto>>>? Callback;
        protected override object? Invoke(System.Reflection.MethodInfo? method, object?[]? args)
        {
            Command = (HRM.Application.Features.PLM.SampleRequests.Commands.SendSampleRequestMessage.SendSampleRequestMessageCommand)args![0]!;
            return Callback is null ? Task.FromResult(Result) : Callback();
        }
    }

    private static RetryWorkflowNotificationJobCommandHandler Handler(ApplicationDbContext db, User user)
        => new(db, user, new CurrentUserPermissionService(user), NullLogger<RetryWorkflowNotificationJobCommandHandler>.Instance);

    private sealed record User(Guid Company) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid? EmployeeId { get; } = Guid.NewGuid();
        public Guid? CompanyId => Company;
        public string? UserName => "isolated-test";
        public string? Email => null;
        public IReadOnlyCollection<string> Roles => [ApplicationRoles.Developer];
        public bool IsInRole(string role) => Roles.Contains(role);
    }
}
