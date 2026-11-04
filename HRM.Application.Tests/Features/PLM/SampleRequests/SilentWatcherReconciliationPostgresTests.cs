using System.Text.Json;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.InternalMail.Services;
using HRM.Application.Features.PLM.SampleRequests.Services;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using HRM.Application.Tests.Features.InternalMail.GroupStorage;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SilentWatcherReconciliationPostgresTests(IsolatedGroupStoragePostgres postgres) : IClassFixture<IsolatedGroupStoragePostgres>
{
    [PostgresGroupStorageFact]
    public async Task PreviewRestoreStalePlansRepeatAndConcurrentInsertRespectExistingPreferencesAndCompany()
    {
        await using var connection = await postgres.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        // Only a disposable cluster is used. No API configuration or application database connection is read.
        foreach (var name in new[] { "SampleRequests", "Products", "Categories", "Quotations", "QuotationLines", "Employees", "Users", "Roles", "UserRoles",
            "Groups", "MemberInGroups", "InternalConversations", "InternalConversationParticipants", "SampleRequestWorkflowEvents", "OutboxMessages" })
        {
            var clr = db.GetType().GetProperty(name)!.PropertyType.GetGenericArguments()[0];
            var type = db.Model.FindEntityType(clr)!;
            var store = StoreObjectIdentifier.Table(type.GetTableName()!, type.GetSchema());
            var schema = store.Schema ?? "public";
            var columns = type.GetProperties().Select(p => $"\"{p.GetColumnName(store)}\" {p.GetRelationalTypeMapping().StoreType} NULL");
            var keys = type.FindPrimaryKey()!.Properties.Select(p => $"\"{p.GetColumnName(store)}\"");
            // All DDL identifiers/types are from the trusted EF model, never from caller input.
            await using var create = new NpgsqlCommand($"CREATE SCHEMA IF NOT EXISTS \"{schema}\"; CREATE TABLE \"{schema}\".\"{store.Name}\" ({string.Join(',', columns)}, PRIMARY KEY ({string.Join(',', keys)}))", connection);
            await create.ExecuteNonQueryAsync();
        }
        var company = Guid.NewGuid(); var sender = Guid.NewGuid(); var root = Guid.NewGuid(); var sample = Guid.NewGuid();
        var product = Guid.NewGuid(); var category = Guid.NewGuid(); var evt = Guid.NewGuid(); var labRole = Guid.NewGuid(); var adminRole = Guid.NewGuid();
        var missing = Guid.NewGuid(); var removed = Guid.NewGuid(); var existing = Guid.NewGuid(); var protectedPerson = Guid.NewGuid(); var watcher = Guid.NewGuid();

        async Task Insert(string set, params (string Property, object Value)[] fields)
        {
            var clr = db.GetType().GetProperty(set)!.PropertyType.GetGenericArguments()[0]; var type = db.Model.FindEntityType(clr)!;
            var table = StoreObjectIdentifier.Table(type.GetTableName()!, type.GetSchema());
            var columns = fields.Select(f => $"\"{type.FindProperty(f.Property)!.GetColumnName(table)}\"");
            await using var command = new NpgsqlCommand($"INSERT INTO \"{table.Schema ?? "public"}\".\"{table.Name}\" ({string.Join(',', columns)}) VALUES ({string.Join(',', fields.Select((_, i) => "@p" + i))})", connection);
            for (var i = 0; i < fields.Length; i++)
            {
                var parameter = command.Parameters.AddWithValue("p" + i, fields[i].Value);
                if (type.FindProperty(fields[i].Property)!.GetColumnType() == "jsonb") parameter.NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Jsonb;
            }
            await command.ExecuteNonQueryAsync();
        }
        async Task AddLab(Guid id, Guid role, string name)
        {
            var account = Guid.NewGuid();
            await Insert("Employees", ("EmployeeId", id), ("CompanyId", company), ("IsActive", true), ("FullName", name), ("ExternalId", "TEST_" + id.ToString("N")));
            await Insert("Users", ("Id", account), ("EmployeeId", id), ("NormalizedUserName", "WATCHERTEST" + id.ToString("N")));
            await Insert("UserRoles", ("UserId", account), ("RoleId", role), ("IsActive", true));
        }
        await Insert("Roles", ("Id", labRole), ("Name", "LabUser"), ("NormalizedName", "LABUSER"));
        await Insert("Roles", ("Id", adminRole), ("Name", "LabAdmin"), ("NormalizedName", "LABADMIN"));
        await Insert("Employees", ("EmployeeId", sender), ("CompanyId", company), ("IsActive", true), ("FullName", "Original sender"));
        await AddLab(missing, labRole, "Missing watcher"); await AddLab(removed, labRole, "Removed watcher");
        await AddLab(existing, labRole, "Existing member"); await AddLab(protectedPerson, adminRole, "Notification recipient");
        await AddLab(watcher, labRole, "Existing watcher");
        await Insert("Categories", ("CategoryId", category), ("CompanyId", company), ("ExternalId", "CMP"));
        await Insert("Products", ("ProductId", product), ("CompanyId", company), ("IsActive", true), ("CategoryId", category));
        await Insert("SampleRequests", ("SampleRequestId", sample), ("CompanyId", company), ("ExternalId", "TP_TEST"), ("IsActive", true), ("ManagerBy", sender), ("ProductId", product));
        await Insert("InternalConversations", ("InternalConversationId", root), ("CompanyId", company), ("RelatedId", sample), ("RelatedType", 5), ("IsActive", true));
        foreach (var (id, active, role, muted) in new[] { (removed, false, 1, false), (existing, true, 1, false), (watcher, true, 2, true) })
            await Insert("InternalConversationParticipants", ("InternalConversationId", root), ("EmployeeId", id), ("IsActive", active), ("Role", role), ("IsMuted", muted), ("IsArchived", true), ("JoinedAt", new DateTime(2026, 10, 1)));
        await Insert("SampleRequestWorkflowEvents", ("Id", evt), ("CompanyId", company), ("SampleRequestId", sample), ("EventType", "SampleRequestWorkflowCreated"), ("RecordedByEmployeeId", sender), ("RecordedAt", new DateTime(2026, 10, 1)));
        var payload = JsonSerializer.Serialize(new { CompanyId = company, EventId = evt, IsInitialMessage = true, UseDefaultSilentWatchers = true });
        await Insert("OutboxMessages", ("Id", 55L), ("Type", "SampleRequestWorkflowNotification"), ("PayloadJson", payload), ("CreatedAt", new DateTime(2026, 10, 1)), ("Attempts", 1));
        SilentWatcherReconciliationService Service(ApplicationDbContext context, Guid tenant) {
            var user = new User(tenant); return new(context, context, user, new CurrentUserPermissionService(user), new SampleRequestRecipientResolver(context),
                new InternalMailAreaAccessService(context, user), NullLogger<SilentWatcherReconciliationService>.Instance);
        }
        var service = Service(db, company);
        var other = await Service(db, Guid.NewGuid()).ExecuteAsync(55, null, false, default);
        Assert.Equal("Unavailable", other!.Outcome);
        var preview = await service.ExecuteAsync(55, null, false, default);
        Assert.Equal("Preview", preview!.Outcome); Assert.Equal(1, preview.MissingCount);
        Assert.Equal("Removed", preview.Rows.Single(r => r.EmployeeId == removed).Status);
        Assert.Equal("NotificationRecipient", preview.Rows.Single(r => r.EmployeeId == protectedPerson).Status);
        Assert.Equal("ExistingMember", preview.Rows.Single(r => r.EmployeeId == existing).Status);
        Assert.Equal("ExistingWatcher", preview.Rows.Single(r => r.EmployeeId == watcher).Status);
        Assert.Equal(3, await db.InternalConversationParticipants.CountAsync());
        Assert.Equal("PreviewChanged", (await service.ExecuteAsync(55, new string('0', 64), true, default))!.Outcome);
        Assert.Equal(3, await db.InternalConversationParticipants.CountAsync());
        // Two independent callers approve the same preview. The shared company/root lock serializes them.
        await using var secondConnection = await postgres.OpenAsync();
        await using var secondDb = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(secondConnection).Options);
        var results = await Task.WhenAll(service.ExecuteAsync(55, preview.Fingerprint, true, default),
            Service(secondDb, company).ExecuteAsync(55, preview.Fingerprint, true, default));
        Assert.Equal(1, results.Sum(r => r!.AddedCount));
        Assert.Contains(results, r => r!.Outcome == "AlreadyCurrent");
        var participants = await db.InternalConversationParticipants.AsNoTracking().ToListAsync();
        var added = Assert.Single(participants, p => p.EmployeeId == missing);
        Assert.True(added.IsActive); Assert.True(added.IsMuted); Assert.False(added.IsArchived);
        Assert.Equal(HRM.Domain.Enums.InternalMailEnums.InternalConversationParticipantRole.Watcher, added.Role);
        Assert.False(participants.Single(p => p.EmployeeId == removed).IsActive);
        Assert.False(participants.Single(p => p.EmployeeId == existing).IsMuted);
        Assert.True(participants.Single(p => p.EmployeeId == existing).IsArchived);
        Assert.DoesNotContain(participants, p => p.EmployeeId == protectedPerson);
        Assert.Equal(4, participants.Count);
        // Explicitly saved [] is not interpreted as omitted defaults.
        await using var explicitEmpty = new NpgsqlCommand("UPDATE notification.outbox_messages SET payload_json=jsonb_set(payload_json,'{SilentWatchers}','[]'::jsonb)", connection);
        await explicitEmpty.ExecuteNonQueryAsync();
        Assert.Empty((await service.ExecuteAsync(55, null, false, default))!.Rows);
        var inactive = Guid.NewGuid(); var foreign = Guid.NewGuid();
        await Insert("Employees", ("EmployeeId", inactive), ("CompanyId", company), ("IsActive", false), ("FullName", "Inactive employee"));
        await Insert("Employees", ("EmployeeId", foreign), ("CompanyId", Guid.NewGuid()), ("IsActive", true), ("FullName", "Foreign employee"));
        await using var explicitList = new NpgsqlCommand("UPDATE notification.outbox_messages SET payload_json=jsonb_set(payload_json,'{SilentWatchers}',@ids::jsonb)", connection);
        explicitList.Parameters.AddWithValue("ids", JsonSerializer.Serialize(new[] { inactive, foreign, protectedPerson }));
        await explicitList.ExecuteNonQueryAsync();
        var restricted = await service.ExecuteAsync(55, null, false, default);
        Assert.DoesNotContain(restricted!.Rows, row => row.EmployeeId == foreign);
        Assert.Equal("Ineligible", restricted.Rows.Single(row => row.EmployeeId == inactive).Status);
        Assert.Equal("NotificationRecipient", restricted.Rows.Single(row => row.EmployeeId == protectedPerson).Status);
        Assert.Equal(0, restricted.MissingCount);
        Assert.Equal("AlreadyCurrent", (await service.ExecuteAsync(55, restricted.Fingerprint, true, default))!.Outcome);
        Assert.Equal(4, await db.InternalConversationParticipants.CountAsync());
        // No read states, notification, outbox insert or group mapping tables exist: an accidental side effect would fail the test.
        Assert.Equal(1, await db.OutboxMessages.CountAsync());
    }

    private sealed record User(Guid Company) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid? CompanyId => Company;
        public Guid? EmployeeId { get; } = Guid.NewGuid();
        public string? UserName => null;
        public string? Email => null;
        public IReadOnlyCollection<string> Roles => [ApplicationRoles.Developer];
        public bool IsInRole(string role) => Roles.Contains(role);
    }
}
