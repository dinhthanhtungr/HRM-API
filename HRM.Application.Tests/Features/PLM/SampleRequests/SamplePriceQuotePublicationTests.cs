using System.Reflection;
using System.Text.Json;
using HRM.Application.Commons.Models;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Application.Features.InternalMail.Services;
using HRM.Application.Features.Notifications.Dtos;
using HRM.Application.Features.Notifications.Services;
using HRM.Application.Features.PLM.SampleRequests.Commands.SendSampleRequestMessage;
using HRM.Application.Features.PLM.SampleRequests.PriceQuoteRequests;
using HRM.Application.Tests.Features.InternalMail.GroupStorage;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.InternalMailEnums;
using HRM.Domain.Enums.SampleRequests;
using HRM.Domain.Identity;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using static HRM.Application.Tests.Features.PLM.SampleRequests.SampleWorkDecisionPostgresTests;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SamplePriceQuotePublicationTests(IsolatedGroupStoragePostgres postgres)
    : IClassFixture<IsolatedGroupStoragePostgres>
{
    [PostgresGroupStorageFact]
    public async Task PriceRequest_WritesOnlyPricing_PreservesAction_AndDoesNotGrantLabMembership()
    {
        await using var connection = await postgres.OpenAsync();
        await using var db = new FixtureDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        var names = new[] { "Employee", "ApplicationUser", "ApplicationRole", "ApplicationUserRole", "IdentityRoleClaim`1",
            "InternalConversation", "InternalConversationParticipant", "InternalMessage", "InternalMessageReadState",
            "SampleRequest", "Product", "Category", "Customer", "Quotation", "QuotationLine", "Group", "MemberInGroup" };
        foreach (var entity in db.Model.GetEntityTypes().Where(e => names.Contains(e.ClrType.Name) && e.FindPrimaryKey() != null)
                     .DistinctBy(e => (e.GetSchema(), e.GetTableName())))
        {
            await CreateTable(connection, entity);
            var table = Microsoft.EntityFrameworkCore.Metadata.StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
            foreach (var property in entity.GetProperties().Where(p => p.ClrType == typeof(bool)))
            {
                var value = property.GetDefaultValue() is true ? "true" : "false";
                await using var defaults = new Npgsql.NpgsqlCommand($"ALTER TABLE \"{table.Schema}\".\"{table.Name}\" ALTER COLUMN \"{property.GetColumnName(table)}\" SET DEFAULT {value}", connection);
                await defaults.ExecuteNonQueryAsync();
            }
        }

        var user = new SampleTrialFeedbackCorrectionTests.User("LabUser");
        var company = user.CompanyId!.Value; var actor = user.EmployeeId!.Value;
        var president = Guid.NewGuid(); var rootId = Guid.NewGuid(); var requestId = Guid.NewGuid();
        foreach (var employee in new[] { actor, president })
            await Insert(connection, db.Model.FindEntityType(typeof(Employee))!, new()
            { ["EmployeeId"] = employee, ["CompanyId"] = company, ["IsActive"] = true });
        var roleId = Guid.NewGuid(); var account = Guid.NewGuid();
        await Insert(connection, db.Model.FindEntityType(typeof(ApplicationRole))!, new()
        { ["Id"] = roleId, ["Name"] = "President", ["NormalizedName"] = "PRESIDENT" });
        await Insert(connection, db.Model.FindEntityType(typeof(ApplicationUser))!, new()
        { ["Id"] = account, ["EmployeeId"] = president });
        await Insert(connection, db.Model.FindEntityType(typeof(ApplicationUserRole))!, new()
        { ["UserId"] = account, ["RoleId"] = roleId, ["IsActive"] = true });
        await Insert(connection, db.Model.FindEntityType(typeof(InternalConversation))!, new()
        { ["InternalConversationId"] = rootId, ["CompanyId"] = company, ["RelatedType"] = InternalMailRelatedType.SampleRequest,
          ["RelatedId"] = requestId, ["RelatedExternalId"] = "TP_TEST", ["IsActive"] = true,
          ["CreatedBy"] = actor, ["CreatedAt"] = DateTime.Now, ["LastMessageAt"] = DateTime.Now });
        await Insert(connection, db.Model.FindEntityType(typeof(InternalConversationParticipant))!, new()
        { ["InternalConversationId"] = rootId, ["EmployeeId"] = actor, ["IsActive"] = true });
        var customerId = Guid.NewGuid(); var productId = Guid.NewGuid(); var categoryId = Guid.NewGuid();
        await Insert(connection, db.Model.FindEntityType(typeof(Customer))!, new()
        { ["CustomerId"] = customerId, ["CompanyId"] = company, ["ExternalId"] = "SYNTHETIC_CUSTOMER", ["IsActive"] = true });
        var productEntity = db.Model.GetEntityTypes().Single(e => e.ClrType.Name == "Product");
        await Insert(connection, db.Model.GetEntityTypes().Single(e => e.ClrType.Name == "Category"), new()
        { ["CategoryId"] = categoryId, ["CompanyId"] = company, ["IsActive"] = true, ["ExternalId"] = "CMB" });
        await Insert(connection, productEntity, new()
        { ["ProductId"] = productId, ["CompanyId"] = company, ["IsActive"] = true, ["CategoryId"] = categoryId });
        await Insert(connection, db.Model.FindEntityType(typeof(SampleRequest))!, new()
        { ["SampleRequestId"] = requestId, ["CompanyId"] = company, ["ProductId"] = productId,
          ["CustomerId"] = customerId, ["IsActive"] = true, ["CreatedDate"] = DateTime.Now, ["ExternalId"] = "TP_TEST" });
        var root = await db.InternalConversations.SingleAsync();
        var service = DispatchProxy.Create<INotificationService, NotificationCapture>();
        var handler = new SendSampleRequestMessageCommandHandler(db, user, service, null!, new(db, user), null!, null!);
        var command = new SendSampleRequestMessageCommand { SampleRequestId = requestId,
            Type = SampleRequestNotificationType.PriceQuoteRequest, Message = "SYNTHETIC PRICE",
            NotificationRecipientEmployeeIdsOverride = [president, actor],
            PriceQuoteRequest = new SampleRequestPriceQuotePayload { SampleRequestId = requestId,
                FormulaSelectionSource = "Explicit", FormulaId = Guid.NewGuid() } };
        var result = await handler.Handle(command, default);
        Assert.True(result.Success, result.Message);
        var group = await db.InternalConversations.Include(c => c.Participants).SingleAsync(c => c.RelatedType == InternalMailRelatedType.ConversationPricing);
        var message = await db.InternalMessages.SingleAsync();
        Assert.Equal(group.InternalConversationId, message.InternalConversationId);
        Assert.DoesNotContain(group.Participants, p => p.EmployeeId == actor);
        Assert.Contains(group.Participants, p => p.EmployeeId == president);
        Assert.False(await db.InternalMessages.AnyAsync(m => m.InternalConversationId == rootId));
        using var payload = JsonDocument.Parse(message.PayloadJson!);
        Assert.Equal("SampleRequestPriceQuoteRequested", payload.RootElement.GetProperty("contentType").GetString());
        Assert.Equal(group.InternalConversationId, payload.RootElement.GetProperty("conversationId").GetGuid());
        Assert.Equal(requestId, payload.RootElement.GetProperty("priceQuoteRequest").GetProperty("sampleRequestId").GetGuid());
        var notification = Assert.Single(((NotificationCapture)service).Requests);
        Assert.Equal(group.InternalConversationId, notification.ConversationId);
        Assert.Equal(new[] { president }, notification.TargetUserIds);
        Assert.Equal(rootId, group.RelatedId);
        var pendingPricing = await HRM.Application.Features.Executive.SampleRequestPricingOverview.GetSampleRequestPricingOverviewQueryHandler
            .SelectLabPriceQuoteRequests(db.InternalConversations.AsNoTracking(), company).ToArrayAsync();
        Assert.Equal(requestId, Assert.Single(pendingPricing).SampleRequestId);
        group.Participants.Single(p => p.EmployeeId == president).IsMuted = true;
        await db.SaveChangesAsync();
        var second = await handler.Handle(command, default);
        Assert.True(second.Success, second.Message);
        Assert.Empty(((NotificationCapture)service).Requests.Last().TargetUserIds);
        Assert.Equal(2, await db.InternalMessages.CountAsync());
        Assert.Equal(1, await db.InternalConversations.CountAsync(c => c.RelatedType == InternalMailRelatedType.ConversationPricing));
        Assert.False(await db.InternalMessages.AnyAsync(m => m.InternalConversationId == rootId));
    }

    public class NotificationCapture : DispatchProxy
    {
        public List<PublishNotificationRequest> Requests { get; } = [];
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name != nameof(INotificationService.PublishAsync)) throw new NotSupportedException();
            Requests.Add((PublishNotificationRequest)args![0]!);
            return Task.FromResult(Guid.NewGuid());
        }
    }

    private sealed class FixtureDbContext(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.HasDefaultSchema("public");
        }
    }
}
