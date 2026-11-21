using System.Text.Json;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.Notifications;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleStandardPriceApprovalTests
{
    [Fact]
    public void CurrentFormulaTargetQueryTranslatesOnPostgresWithoutConnectingToDatabase()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var service = new ProductPricingApprovalNotificationService(db, null!, null!);
        var sql = service.ApprovalTargets(Price(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()))
            .Select(r => new { r.SampleRequestId, r.RequestType, r.Customer.ExternalId }).ToQueryString();
        Assert.Contains("SELECT", sql);
        Assert.Contains("WHERE", sql);
    }
    [Fact]
    public async Task QueueIsAtomicScopedToCurrentFormulaAndIdempotentPerVersionAndRequest()
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<ICRMWriteDbContext>();
        var company = Guid.NewGuid(); var product = Guid.NewGuid(); var formula = Guid.NewGuid();
        var request = Request(company, product, formula);
        var otherCompany = Request(Guid.NewGuid(), product, formula);
        var otherFormula = Request(company, product, Guid.NewGuid());
        var privateRequest = Request(company, product, formula); privateRequest.RequestType = "private";
        var inactive = Request(company, product, formula); inactive.IsActive = false;
        store.Seed(nameof(db.SampleRequests), request, otherCompany, otherFormula, privateRequest, inactive);
        store.Seed(nameof(db.Formulas), new Formula { FormulaId = formula, CompanyId = company, ProductId = product, IsActive = true });
        var price = Price(company, product, formula);
        var service = new ProductPricingApprovalNotificationService(db, null!, null!);
        await service.QueueSampleRequestApprovalsAsync(price, default);
        await service.QueueSampleRequestApprovalsAsync(price, default);
        var evt = Assert.Single(store.Added.OfType<SampleRequestWorkflowEvent>());
        Assert.Equal(request.SampleRequestId, evt.SampleRequestId);
        Assert.Equal(price.ProductPricingVersionId, evt.ProductPricingVersionId);
        Assert.Equal(SampleFlowRules.StandardPriceApproved, evt.EventType);
        Assert.Null(evt.Note);
        Assert.False(SampleRequestPriceFeedbackVisibility.IsPriceEvent(evt.EventType));
        Assert.True(SampleRequestPriceFeedbackVisibility.IsPrivatePublication(evt.EventType));
        Assert.Equal(0, store.Saves); // Approval owner commits once; queue must not save separately.
        var signal = Assert.Single(store.Added.OfType<OutboxMessage>(), j => j.Type == SampleWorkRealtimeSignal.OutboxType);
        using var payload = JsonDocument.Parse(signal.PayloadJson);
        Assert.Single(payload.RootElement.EnumerateObject());
        Assert.Equal(company, payload.RootElement.GetProperty("CompanyId").GetGuid());
        Assert.Single(store.Added.OfType<OutboxMessage>(), j => j.Type == SampleRequestWorkflowNotificationQueue.OutboxType);
    }

    [Fact]
    public async Task PersistedEventPreventsRetryFromCreatingAnyNewWork()
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<ICRMWriteDbContext>();
        var company = Guid.NewGuid(); var product = Guid.NewGuid(); var formula = Guid.NewGuid();
        var request = Request(company, product, formula); var price = Price(company, product, formula);
        store.Seed(nameof(db.SampleRequests), request);
        store.Seed(nameof(db.Formulas), new Formula { FormulaId = formula, CompanyId = company, ProductId = product, IsActive = true });
        store.Seed(nameof(db.SampleRequestWorkflowEvents), new SampleRequestWorkflowEvent {
            Id = ProductPricingApprovalNotificationService.ApprovalEventId(price.ProductPricingVersionId, request.SampleRequestId), CompanyId = company });
        await new ProductPricingApprovalNotificationService(db, null!, null!).QueueSampleRequestApprovalsAsync(price, default);
        Assert.Empty(store.Added);
        Assert.NotEqual(ProductPricingApprovalNotificationService.ApprovalEventId(price.ProductPricingVersionId, request.SampleRequestId),
            ProductPricingApprovalNotificationService.ApprovalEventId(Guid.NewGuid(), request.SampleRequestId));
    }

    [Fact]
    public void MessageContainsSellingPricesAndOrderedActiveTiersWithoutPrivateCostNotes()
    {
        var price = Price(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        price.Note = "SECRET_INTERNAL"; price.PublisherNote = "SECRET_PUBLISHER";
        price.PriceTiers.Add(new ProductPricingTier { SortOrder = 2, MinQuantity = 100, UnitPrice = 34000 });
        price.PriceTiers.Add(new ProductPricingTier { SortOrder = 1, MaxQuantity = 100, MaxInclusive = false, UnitPrice = 35000 });
        price.PriceTiers.Add(new ProductPricingTier { SortOrder = 0, UnitPrice = 999, IsActive = false });
        var text = ProductPricingApprovalNotificationService.ApprovalText(price);
        Assert.Contains("Giá chuẩn: 36.000 VND/kg", text);
        Assert.True(text.IndexOf("35.000", StringComparison.Ordinal) < text.IndexOf("34.000", StringComparison.Ordinal));
        Assert.DoesNotContain("SECRET", text); Assert.DoesNotContain("999", text);
        Assert.True(SampleRequestWorkflowPublicationRules.ShouldDispatch(new(2026, 10, 1), SampleFlowRules.StandardPriceApproved));
    }

    private static SampleRequest Request(Guid company, Guid product, Guid formula) => new() {
        SampleRequestId = Guid.NewGuid(), CompanyId = company, ProductId = product, FormulaId = formula, IsActive = true,
        CreatedDate = new(2026, 11, 9),
        Customer = new Customer { ExternalId = "KH_TEST" }, RequestType = "Báo giá" };
    private static ProductPricingVersion Price(Guid company, Guid product, Guid formula) => new() {
        ProductPricingVersionId = Guid.NewGuid(), CompanyId = company, ProductId = product, SourceFormulaId = formula,
        Status = ProductPricingStatus.Approved, StandardSellingPrice = 36000, Currency = "VND", Version = 2,
        ApprovedBy = Guid.NewGuid(), ApprovedAt = new(2026, 11, 9, 10, 30, 0) };
}
