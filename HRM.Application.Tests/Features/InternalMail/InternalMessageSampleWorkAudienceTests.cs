using HRM.Application.Features.InternalMail.Dtos;
using HRM.Application.Features.InternalMail.Services;
using HRM.Application.Tests.Features.InternalMail.GroupStorage;
using HRM.Application.Tests.Features.PLM.SampleRequests;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.InternalMailEnums;
using HRM.Domain.Identity;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;

namespace HRM.Application.Tests.Features.InternalMail;

public sealed class InternalMessageSampleWorkAudienceTests(IsolatedGroupStoragePostgres postgres)
    : IClassFixture<IsolatedGroupStoragePostgres>
{
    [Theory]
    [InlineData("SaleUser", "Sale")]
    [InlineData("SALEADMIN", "Sale")]
    [InlineData("LabUser", "Lab")]
    [InlineData("LabAdmin", "Lab")]
    [InlineData("President", null)]
    [InlineData("Developer", null)]
    public void RoleHintIsBusinessSideNotAPermission(string role, string? expected)
        => Assert.Equal(expected, InternalMessageSampleWorkAudience.FromRoles([role]));

    [Fact]
    public void MixedOrMissingBusinessRolesStayUnknown()
    {
        Assert.Null(InternalMessageSampleWorkAudience.FromRoles(["SaleAdmin", "LabUser", "Developer"]));
        Assert.Null(InternalMessageSampleWorkAudience.FromRoles([]));
    }

    [Theory]
    [InlineData("LabFormulaConfirmed", "Lab")]
    [InlineData("LabNoteRecorded", "Lab")]
    [InlineData("SampleSent", "Lab")]
    [InlineData("ChipFormulaFinalized", "Lab")]
    [InlineData("QuotationFormulaFinalized", "Lab")]
    [InlineData("CustomerPriceAccepted", "Sale")]
    [InlineData("CustomerFeedbackRecorded", "Sale")]
    [InlineData("LabFinalizationRequested", "Sale")]
    [InlineData("TrialSampleRequested", "Sale")]
    [InlineData("SampleRequestCancelled", null)]
    [InlineData("UnknownEvent", null)]
    public void CanonicalWorkflowOriginDisambiguatesMixedActors(string type, string? expected)
        => Assert.Equal(expected, InternalMessageSampleWorkAudience.FromEvent(type));

    [PostgresGroupStorageFact]
    public async Task LoadedPageIsDecoratedInBatchesWithStrictConversationActorAndCompanyLinks()
    {
        await using var connection = await postgres.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        foreach (var type in new[] { typeof(InternalConversation), typeof(SampleRequest), typeof(SampleRequestWorkflowEvent),
                     typeof(Employee), typeof(ApplicationUser), typeof(ApplicationRole), typeof(ApplicationUserRole) })
        {
            var entity = db.Model.FindEntityType(type)!;
            var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
            var schema = table.Schema ?? "public";
            var columns = entity.GetProperties().Select(p =>
            {
                var defaultSql = p.TryGetDefaultValue(out var value) && value is not null
                    ? " DEFAULT " + p.GetRelationalTypeMapping().GenerateSqlLiteral(value) : "";
                return $"\"{p.GetColumnName(table)}\" {p.GetRelationalTypeMapping().StoreType}{defaultSql}";
            });
            var keys = entity.FindPrimaryKey()!.Properties.Select(p => $"\"{p.GetColumnName(table)}\"");
            // Trusted EF metadata in the verified disposable cluster only; no application DB or real records.
            await using var create = new NpgsqlCommand($"CREATE SCHEMA IF NOT EXISTS \"{schema}\"; CREATE TABLE \"{schema}\".\"{table.Name}\" ({string.Join(',', columns)}, PRIMARY KEY ({string.Join(',', keys)}));", connection);
            await create.ExecuteNonQueryAsync();
        }
        var user = new SampleTrialFeedbackCorrectionTests.User("Developer");
        var company = user.CompanyId!.Value;
        var request = new SampleRequest { SampleRequestId = Guid.NewGuid(), CompanyId = company, IsActive = true, CreatedDate = new(2026, 11, 6) };
        var otherRequest = new SampleRequest { SampleRequestId = Guid.NewGuid(), CompanyId = company, IsActive = true };
        var root = new InternalConversation { InternalConversationId = Guid.NewGuid(), CompanyId = company,
            RelatedType = InternalMailRelatedType.SampleRequest, RelatedId = request.SampleRequestId, IsActive = true };
        var child = new InternalConversation { InternalConversationId = Guid.NewGuid(), CompanyId = company,
            RelatedType = InternalMailRelatedType.ConversationPricing, RelatedId = root.InternalConversationId, IsActive = true };
        var nonSample = new InternalConversation { InternalConversationId = Guid.NewGuid(), CompanyId = company,
            RelatedType = InternalMailRelatedType.Customer, RelatedId = Guid.NewGuid(), IsActive = true };
        var outsider = new InternalConversation { InternalConversationId = Guid.NewGuid(), CompanyId = Guid.NewGuid(),
            RelatedType = InternalMailRelatedType.SampleRequest, RelatedId = request.SampleRequestId, IsActive = true };
        var legacyRequest = new SampleRequest { SampleRequestId = Guid.NewGuid(), CompanyId = company, IsActive = true, CreatedDate = new(2026, 10, 31) };
        var legacyRoot = new InternalConversation { InternalConversationId = Guid.NewGuid(), CompanyId = company,
            RelatedType = InternalMailRelatedType.SampleRequest, RelatedId = legacyRequest.SampleRequestId, IsActive = true };
        var boundaryRequest = new SampleRequest { SampleRequestId = Guid.NewGuid(), CompanyId = company, IsActive = true, CreatedDate = new(2026, 11, 1) };
        var boundaryRoot = new InternalConversation { InternalConversationId = Guid.NewGuid(), CompanyId = company,
            RelatedType = InternalMailRelatedType.SampleRequest, RelatedId = boundaryRequest.SampleRequestId, IsActive = true };
        db.SampleRequests.AddRange(request, otherRequest, legacyRequest, boundaryRequest);
        var sale = AddSender("SaleAdmin");
        var lab = AddSender("LabUser");
        var mixed = AddSender("SaleUser", "LabUser", "Developer");
        var inactive = AddSender("SaleUser");
        db.Employees.Local.Single(x => x.EmployeeId == inactive).IsActive = false;
        var otherCompany = AddSender("LabUser");
        db.Employees.Local.Single(x => x.EmployeeId == otherCompany).CompanyId = Guid.NewGuid();
        var saleMessage = Message(sale);
        var labMessage = Message(lab);
        var mixedMessage = Message(mixed, child.InternalConversationId);
        var wrongRequest = Message(mixed);
        var wrongActor = Message(lab);
        var wrongConversation = Message(mixed);
        var deleted = Message(sale); deleted.IsDeleted = true;
        var nonSampleMessage = Message(sale, nonSample.InternalConversationId);
        var outsideMessage = Message(sale, outsider.InternalConversationId);
        var inactiveMessage = Message(inactive);
        var outsideEmployeeMessage = Message(otherCompany);
        var conflicting = Message(sale);
        var legacyMessage = Message(sale, legacyRoot.InternalConversationId);
        legacyMessage.SentAt = new(2026, 11, 6); // A new message does not make an old request eligible.
        var boundaryMessage = Message(sale, boundaryRoot.InternalConversationId);
        AddEvent(mixedMessage, "CustomerPriceAccepted");
        AddEvent(wrongRequest, "LabNoteRecorded", requestId: otherRequest.SampleRequestId);
        AddEvent(wrongActor, "CustomerPriceAccepted", actor: sale);
        AddEvent(wrongConversation, "LabNoteRecorded", conversation: child.InternalConversationId);
        AddEvent(conflicting, "LabNoteRecorded");
        AddEvent(conflicting, "CustomerPriceAccepted");
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        foreach (var conversation in new[] { root, child, nonSample, outsider, legacyRoot, boundaryRoot })
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"InternalMail\".\"InternalConversations\" (\"InternalConversationId\", \"CompanyId\", \"Subject\", \"RelatedType\", \"RelatedId\", \"CreatedBy\", \"CreatedAt\", \"LastMessageAt\", \"IsActive\") VALUES ({conversation.InternalConversationId}, {conversation.CompanyId}, {conversation.Subject}, {(int?)conversation.RelatedType}, {conversation.RelatedId}, {Guid.Empty}, {DateTime.UtcNow}, {DateTime.UtcNow}, true)");
        var messages = new[] { saleMessage, labMessage, mixedMessage, wrongRequest, wrongActor, wrongConversation,
            deleted, nonSampleMessage, outsideMessage, inactiveMessage, outsideEmployeeMessage, conflicting, legacyMessage, boundaryMessage };
        await InternalMessageSampleWorkAudience.AttachAsync(messages, db, new InternalMailAreaAccessService(db, user), company, default);
        Assert.Equal("Sale", saleMessage.SenderSampleWorkAudience);
        Assert.Equal("Lab", labMessage.SenderSampleWorkAudience);
        Assert.Equal("Sale", mixedMessage.SenderSampleWorkAudience);
        Assert.Equal("Lab", wrongActor.SenderSampleWorkAudience);
        Assert.True(saleMessage.SampleWorkTrackingEligible);
        Assert.True(mixedMessage.SampleWorkTrackingEligible);
        Assert.False(legacyMessage.SampleWorkTrackingEligible);
        Assert.True(boundaryMessage.SampleWorkTrackingEligible);
        Assert.Null(outsideMessage.SampleWorkTrackingEligible);
        Assert.Null(nonSampleMessage.SampleWorkTrackingEligible);
        Assert.All(new[] { wrongRequest, wrongConversation, deleted, nonSampleMessage, outsideMessage, inactiveMessage, outsideEmployeeMessage, conflicting },
            x => Assert.Null(x.SenderSampleWorkAudience));
        // A forwarded copy has no matching event: use its actual sender, not source text or a client payload reference.
        var forwarded = Message(lab);
        forwarded.PayloadJson = "{\"isForwarded\":true,\"WorkflowEventId\":\"" + mixedMessage.MessageId + "\"}";
        await InternalMessageSampleWorkAudience.AttachAsync([forwarded], db, new InternalMailAreaAccessService(db, user), company, default);
        Assert.Equal("Lab", forwarded.SenderSampleWorkAudience);

        Guid AddSender(params string[] roles)
        {
            var employee = new Employee { EmployeeId = Guid.NewGuid(), CompanyId = company, IsActive = true, FullName = "Test sender" };
            var account = new ApplicationUser { Id = Guid.NewGuid(), EmployeeId = employee.EmployeeId };
            db.Employees.Add(employee); db.Users.Add(account);
            foreach (var name in roles)
            {
                var role = new ApplicationRole { Id = Guid.NewGuid(), Name = name, NormalizedName = name.ToUpperInvariant() };
                db.Roles.Add(role); db.UserRoles.Add(new ApplicationUserRole { UserId = account.Id, RoleId = role.Id, IsActive = true });
            }
            return employee.EmployeeId;
        }
        InternalMessageDto Message(Guid sender, Guid? conversation = null) => new()
        { MessageId = Guid.NewGuid(), ConversationId = conversation ?? root.InternalConversationId, SenderEmployeeId = sender };
        void AddEvent(InternalMessageDto message, string type, Guid? requestId = null, Guid? actor = null, Guid? conversation = null)
            => db.SampleRequestWorkflowEvents.Add(new SampleRequestWorkflowEvent
            {
                Id = Guid.NewGuid(), CompanyId = company, SampleRequestId = requestId ?? request.SampleRequestId,
                MessageId = message.MessageId, ConversationId = conversation ?? message.ConversationId,
                RecordedByEmployeeId = actor ?? message.SenderEmployeeId, EventType = type
            });
    }
}
