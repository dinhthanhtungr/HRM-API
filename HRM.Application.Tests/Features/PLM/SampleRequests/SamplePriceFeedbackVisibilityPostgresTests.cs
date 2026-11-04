using System.Text.Json;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.SampleRequests.Dtos.SampleTrials;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestWorkflowEvents;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using HRM.Application.Features.Pricing.Authorization;
using HRM.Application.Tests.Features.InternalMail.GroupStorage;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using static HRM.Application.Tests.Features.PLM.SampleRequests.SampleWorkDecisionPostgresTests;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SamplePriceFeedbackVisibilityPostgresTests(IsolatedGroupStoragePostgres postgres) : IClassFixture<IsolatedGroupStoragePostgres>
{
    [PostgresGroupStorageFact]
    public async Task ScopedReportAndWorkflowHistoryHidePriceFreeTextButKeepTechnicalNotesAndStatus()
    {
        await using var connection = await postgres.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        foreach (var name in new[] { "SampleRequest", "SampleRequestSampleTrial", "Formula", "SampleRequestWorkflowEvent", "Employee",
            "Customer", "CustomerClaim", "CustomerAssignment", "OutboxMessage" })
            await CreateTable(connection, db.Model.GetEntityTypes().Single(x => x.ClrType.Name == name));
        var sale = new SampleTrialFeedbackCorrectionTests.User("SaleUser");
        var company = sale.CompanyId!.Value; var request = Guid.NewGuid(); var formula = Guid.NewGuid();
        var customer = Guid.NewGuid(); var product = Guid.NewGuid(); var now = new DateTime(2026, 11, 4);
        await Insert(connection, db.Model.FindEntityType(typeof(Customer))!, new()
        { ["CustomerId"] = customer, ["CompanyId"] = company, ["IsActive"] = true, ["IsLead"] = true });
        await Insert(connection, db.Model.FindEntityType(typeof(SampleRequest))!, new()
        { ["SampleRequestId"] = request, ["CompanyId"] = company, ["CustomerId"] = customer, ["ProductId"] = product,
          ["ManagerBy"] = sale.EmployeeId, ["FormulaId"] = formula, ["IsActive"] = true,
          ["CreatedDate"] = now, ["Status"] = "InProgress", ["InfoType"] = "ColorCard" });
        await Insert(connection, db.Model.FindEntityType(typeof(Formula))!, new()
        { ["FormulaId"] = formula, ["CompanyId"] = company, ["ProductId"] = product, ["IsActive"] = true, ["Status"] = "Rework" });
        await Insert(connection, db.Model.FindEntityType(typeof(SampleRequestSampleTrial))!, new()
        { ["SampleRequestSampleTrialId"] = Guid.NewGuid(), ["SampleRequestId"] = request, ["FormulaId"] = formula,
          ["IsActive"] = true, ["TrialNo"] = 1, ["DeliveredSampleQuantityKg"] = 0m, ["Status"] = (int)HRM.Domain.Enums.SampleRequests.SampleTrialStatus.Failed,
          ["CustomerReplyDate"] = now, ["CustomerReplyNote"] = "TECHNICAL-ONLY" });
        await Insert(connection, db.Model.FindEntityType(typeof(SampleRequestWorkflowEvent))!, new()
        { ["Id"] = Guid.NewGuid(), ["SampleRequestId"] = request, ["CompanyId"] = company, ["FormulaId"] = formula,
          ["EventType"] = SampleRequestWorkflowEvents.PriceAccepted, ["RecordedAt"] = now,
          ["Note"] = "SECRET-QUOTED-AMOUNT", ["PriceReference"] = "SECRET-REFERENCE" });
        await Insert(connection, db.Model.FindEntityType(typeof(SampleRequestWorkflowEvent))!, new()
        { ["Id"] = Guid.NewGuid(), ["SampleRequestId"] = request, ["CompanyId"] = company,
          ["EventType"] = SampleRequestWorkflowEvents.FeedbackRecorded, ["RecordedAt"] = now, ["Note"] = "TECHNICAL-ONLY" });
        foreach (var user in new ICurrentUser[] { new RoleView(sale, "LabUser"), sale })
        {
            var access = new PricingVisibilityService(new CurrentUserPermissionService(user));
            var canView = access.GetAccess().CanViewApprovedSellingPrice;
            var item = new SampleRequestSampleTrialReportDto { SampleRequestId = request };
            await SampleRequestCustomerPriceAudit.AttachAsync(db, company, [item], default);
            await SampleFlowStateReader.AttachAsync(db, [item], company, user.EmployeeId, true, true, default);
            SampleRequestPriceFeedbackVisibility.Apply(item, access.GetAccess());
            var json = JsonSerializer.Serialize(item);
            Assert.Equal(canView, json.Contains("SECRET-QUOTED-AMOUNT"));
            Assert.Equal(canView, json.Contains("SECRET-REFERENCE"));
            Assert.Contains("TECHNICAL-ONLY", json);
            Assert.Equal("Accepted", Assert.Single(item.FeedbackSummaries, x => x.Kind == "Price").Outcome);
            Assert.True(Assert.Single(item.FeedbackSummaries, x => x.Kind == "Price").IsNoteHidden);
            Assert.Null(Assert.Single(item.FeedbackSummaries, x => x.Kind == "Price").Note);
            var events = await new GetSampleRequestWorkflowEventsQueryHandler(db, user,
                SampleWorkflowPersistencePostgresTests.RestrictedVisibility.Create(user), access)
                .Handle(new GetSampleRequestWorkflowEventsQuery(request), default);
            Assert.NotNull(events);
            var eventJson = JsonSerializer.Serialize(events);
            Assert.Equal(canView, eventJson.Contains("SECRET-QUOTED-AMOUNT"));
            Assert.Contains("TECHNICAL-ONLY", eventJson);
            Assert.Equal(!canView, Assert.Single(events!.Items, x => x.EventType == SampleRequestWorkflowEvents.PriceAccepted).IsNoteHidden);
        }
    }
    private sealed class RoleView(ICurrentUser source, string role) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid UserId => source.UserId;
        public Guid? CompanyId => source.CompanyId;
        public Guid? EmployeeId => source.EmployeeId;
        public string? UserName => null;
        public string? Email => null;
        public IReadOnlyCollection<string> Roles => [role];
        public bool IsInRole(string value) => value == role;
    }
}
