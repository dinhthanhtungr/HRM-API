using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.SampleRequests.Commands.PatchSampleRequest;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.AuditSchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.SampleRequestSchema;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleRequestQuotationConversionTests
{
    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, 10)]
    [InlineData(true, 0)]
    [InlineData(true, 10)]
    public async Task AddsChipDeliveryOnSameRequestPreservingPriceHistory(bool accepted, double kg)
    {
        var (handler, db, store, parent) = Arrange(accepted);
        var originalId = parent.SampleRequestId;
        var result = await handler.Handle(new() { SampleRequestId = originalId, InfoType = "Thẻ màu (3)", SampleQuantity = kg,
            ExpectedUpdatedDate = parent.UpdatedDate }, default);
        Assert.True(result.Success, result.Message);
        Assert.Equal(originalId, parent.SampleRequestId);
        Assert.Equal("Thẻ màu (3)", parent.InfoType);
        Assert.Equal(kg, parent.SampleQuantity);
        Assert.Equal(25000, parent.ExpectedPrice);
        Assert.Equal("InProgress", parent.Status);
        var added = store.Added.OfType<SampleRequestWorkflowEvent>().ToArray();
        Assert.Contains(added, e => e.EventType == SampleFlowRules.QuotationRequired);
        Assert.Contains(added, e => e.EventType == (kg > 0 ? SampleFlowRules.ChipThenSampleRequested : SampleFlowRules.ChipRequested));
        Assert.DoesNotContain(added, e => e.EventType == SampleRequestWorkflowEvents.PriceRevoked);
        Assert.Single(store.Added.OfType<AuditLog>());
        Assert.Empty(store.Added.OfType<SampleRequest>());
        var history = added.Concat(db.SampleRequestWorkflowEvents).OrderByDescending(e => e.RecordedAt).ToArray();
        Assert.True(KgSampleCompletionRules.RequiresPriceAcceptance(parent, history));
        Assert.Equal(kg > 0, ChipThenSampleRules.NeedsChipRecord(parent, null, history, []));
        Assert.Equal(1, store.Saves);
    }

    [Theory]
    [InlineData("Thẻ màu", 10)]
    [InlineData("Thẻ màu (0)", 10)]
    [InlineData("Thẻ màu (31)", 10)]
    [InlineData("Thẻ màu (3)", -1)]
    public async Task RejectsInvalidSetsOrQuantityWithoutMutation(string type, double kg)
    {
        var (handler, _, store, parent) = Arrange(false);
        var result = await handler.Handle(new() { SampleRequestId = parent.SampleRequestId, InfoType = type, SampleQuantity = kg }, default);
        Assert.False(result.Success);
        Assert.Equal("Báo giá", parent.InfoType);
        Assert.Empty(store.Added);
        Assert.Equal(0, store.Saves);
    }

    [Fact]
    public async Task DoesNotUseConversionToChangeAnAcceptedPrice()
    {
        var (handler, _, store, parent) = Arrange(true);
        var result = await handler.Handle(new() { SampleRequestId = parent.SampleRequestId, InfoType = "Thẻ màu (3)", SampleQuantity = 10, ExpectedPrice = 1 }, default);
        Assert.False(result.Success);
        Assert.Contains("Revoke customer price", result.Message);
        Assert.Equal("Báo giá", parent.InfoType);
        Assert.Empty(store.Added);
    }

    [Fact]
    public async Task StaleConversionDoesNotAppendPlanOrChangeType()
    {
        var (handler, _, store, parent) = Arrange(false);
        var result = await handler.Handle(new() { SampleRequestId = parent.SampleRequestId, InfoType = "Thẻ màu (3)", ExpectedUpdatedDate = parent.UpdatedDate!.Value.AddSeconds(-1) }, default);
        Assert.False(result.Success);
        Assert.Equal("Báo giá", parent.InfoType);
        Assert.Empty(store.Added);
    }

    [Theory]
    [InlineData("Completed", false)]
    [InlineData("Cancelled", false)]
    [InlineData("InProgress", true)]
    public async Task KeepsClosedAndAlreadyDeliveredRequestsOnExistingPlanActions(string status, bool delivered)
    {
        var (handler, db, store, parent) = Arrange(false);
        parent.Status = status;
        if (delivered) store.Seed(nameof(db.SampleRequestSampleTrials), new SampleRequestSampleTrial {
            SampleRequestSampleTrialId = Guid.NewGuid(), SampleRequestId = parent.SampleRequestId, IsActive = true, SentDate = DateTime.Now });
        var result = await handler.Handle(new() { SampleRequestId = parent.SampleRequestId, InfoType = "Thẻ màu (3)", SampleQuantity = 10 }, default);
        Assert.False(result.Success);
        Assert.Contains("Change plan", result.Message);
        Assert.Empty(store.Added);
    }

    [Fact]
    public async Task ReverseConversionKeepsExistingRestriction()
    {
        var (handler, _, store, parent) = Arrange(false);
        parent.InfoType = "Thẻ màu (3)";
        var result = await handler.Handle(new() { SampleRequestId = parent.SampleRequestId, InfoType = "Báo giá" }, default);
        Assert.False(result.Success);
        Assert.Contains("revised request", result.Message);
        Assert.Equal("Thẻ màu (3)", parent.InfoType);
        Assert.Empty(store.Added);
    }

    private static (PatchSampleRequestCommandHandler Handler, IPLMWriteDbContext Db, ProductionOrderTestStore Store, SampleRequest Parent) Arrange(bool accepted)
    {
        var user = new SampleTrialFeedbackCorrectionTests.User("SaleUser");
        var (db, store) = ProductionOrderTestStore.CreateContext<IPLMWriteDbContext>();
        var now = new DateTime(2026, 11, 14, 8, 0, 0);
        var parent = new SampleRequest { SampleRequestId = Guid.NewGuid(), CompanyId = user.CompanyId!.Value,
            ManagerBy = user.EmployeeId!.Value, InfoType = "Báo giá", SampleQuantity = 0, ExpectedPrice = 25000,
            Status = "InProgress", CreatedDate = now, UpdatedDate = now, IsActive = true };
        store.Seed(nameof(db.SampleRequests), parent);
        store.Seed(nameof(db.Employees), new Employee { EmployeeId = user.EmployeeId!.Value, CompanyId = user.CompanyId.Value, IsActive = true });
        var events = new List<SampleRequestWorkflowEvent> { SampleRequestCustomerPriceAudit.CreateWorkflowMarker(parent.SampleRequestId, user.CompanyId.Value, user.EmployeeId.Value, now, parent.Status) };
        if (accepted) events.Add(SampleRequestCustomerPriceAudit.CreateEvent(parent.SampleRequestId, user.CompanyId.Value, user.EmployeeId.Value, now.AddMinutes(1), false, parent.Status, parent.Status, now, null, "QA price", "Existing acceptance"));
        store.Seed(nameof(db.SampleRequestWorkflowEvents), events.ToArray());
        return (new PatchSampleRequestCommandHandler(db, user, SampleTrialFeedbackCorrectionTests.Visibility.Create(user), null!, null!, null!, new CurrentUserPermissionService(user)), db, store, parent);
    }
}
