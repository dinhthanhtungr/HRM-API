using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestAdminSummary;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.SampleRequests;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleAdminLabCompletionTests
{
    [Theory]
    [InlineData("Báo giá", 0.1, false, false, false)]
    [InlineData("Quotation", 0.1, true, false, false)]
    [InlineData("Báo giá", 0.1, true, true, true)]
    [InlineData("Báo giá", 0, false, false, true)]
    [InlineData("Quotation", 0, false, false, true)]
    [InlineData("Báo giá", 0, true, true, true)]
    public void RequestedKgRequiresActualKgDeliveryInsteadOfPriceOrChip(string infoType, double kg, bool chipSent, bool kgSent, bool expectedDone)
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<IPLMReadDbContext>();
        var day = new DateTime(2026, 11, 5);
        var request = new SampleRequest { SampleRequestId = Guid.NewGuid(), CompanyId = Guid.NewGuid(), ProductId = Guid.NewGuid(),
            FormulaId = Guid.NewGuid(), CreatedDate = day, Status = "InProgress", IsActive = true, InfoType = infoType, SampleQuantity = kg };
        store.Seed(nameof(db.Formulas), new Formula { FormulaId = request.FormulaId.Value, CompanyId = request.CompanyId, ProductId = request.ProductId, IsActive = true });
        // Even a historical terminal event must not bypass the requested kg delivery.
        store.Seed(nameof(db.SampleRequestWorkflowEvents), new SampleRequestWorkflowEvent { CompanyId = request.CompanyId,
            SampleRequestId = request.SampleRequestId, EventType = SampleFlowRules.CompletedWithoutTrial, RecordedAt = day });
        var trials = new List<SampleRequestSampleTrial>();
        if (chipSent) trials.Add(new() { SampleRequestId = request.SampleRequestId, SampleRequest = request, IsActive = true,
            TrialNo = 1, SentDate = day, DeliveredSampleQuantityKg = 0, Status = SampleTrialStatus.SampleSent });
        if (kgSent) trials.Add(new() { SampleRequestId = request.SampleRequestId, SampleRequest = request, IsActive = true,
            TrialNo = 2, SentDate = day, DeliveredSampleQuantityKg = 0.1m, Status = SampleTrialStatus.SampleSent });
        store.Seed(nameof(db.SampleRequestSampleTrials), trials.ToArray());
        var prices = new[] { new ProductPricingVersion { CompanyId = request.CompanyId, ProductId = request.ProductId,
            SourceFormulaId = request.FormulaId, IsActive = true, Currency = "VND", Status = ProductPricingStatus.Approved,
            StandardSellingPrice = 100, ApprovedAt = day } }.AsQueryable();
        var rows = SampleRequestAdminReportQuery.Build(new[] { request }.AsQueryable(), db, prices);
        Assert.Equal(expectedDone ? 1 : 0, SampleRequestAdminReportQuery.Filter(rows, SampleRequestAdminMetric.Completed, day, day.AddDays(1)).Count());
        if (expectedDone)
        {
            Assert.Equal(kgSent ? "sample" : "quotation", rows.Single().LabCompletedKind);
            Assert.Single(SampleRequestAdminReportQuery.Filter(rows,
                kgSent ? SampleRequestAdminMetric.CompletedSample : SampleRequestAdminMetric.CompletedQuotation, day, day.AddDays(1)));
            Assert.Empty(SampleRequestAdminReportQuery.Filter(rows, SampleRequestAdminMetric.CompletedChip, day, day.AddDays(1)));
        }
        Assert.Equal(expectedDone ? 0 : 1, SampleRequestAdminReportQuery.Filter(rows, SampleRequestAdminMetric.InProgress, day, day.AddDays(1)).Count());
        Assert.Equal("InProgress", request.Status);
    }
    [Theory]
    [InlineData(false, false, false, true)]
    [InlineData(false, true, false, false)]
    [InlineData(false, true, true, true)]
    [InlineData(true, false, false, true)]
    [InlineData(true, true, false, false)]
    [InlineData(true, true, true, true)]
    public void LabDoneIsIndependentOfRequestClosureAndReworkReturnsToWorking(bool quotation, bool rework, bool redelivered, bool expected)
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<IPLMReadDbContext>();
        var day = new DateTime(2026, 11, 5);
        var request = new SampleRequest { SampleRequestId = Guid.NewGuid(), CompanyId = Guid.NewGuid(),
            ProductId = Guid.NewGuid(), FormulaId = Guid.NewGuid(), CreatedDate = day.AddDays(-1), IsActive = true,
            Status = "InProgress", InfoType = quotation ? "Báo giá" : "Thẻ màu" };
        var formula = new Formula { FormulaId = request.FormulaId.Value, ProductId = request.ProductId, CompanyId = request.CompanyId, IsActive = true };
        store.Seed(nameof(db.Formulas), formula);
        store.Seed(nameof(db.SampleRequestWorkflowEvents), rework && quotation
            ? new[] { new SampleRequestWorkflowEvent { CompanyId = request.CompanyId, SampleRequestId = request.SampleRequestId,
                EventType = SampleFlowRules.LowerPriceRequested, RecordedAt = day.AddDays(1) } } : []);
        var trials = new List<SampleRequestSampleTrial>();
        if (!quotation)
        {
            trials.Add(new() { SampleRequestId = request.SampleRequestId, SampleRequest = request, TrialNo = 1,
                IsActive = true, SentDate = day, DeliveredSampleQuantityKg = 0,
                Status = rework ? SampleTrialStatus.Failed : SampleTrialStatus.SampleSent,
                CustomerReplyDate = rework ? day.AddDays(1) : null });
            if (redelivered) trials.Add(new() { SampleRequestId = request.SampleRequestId, SampleRequest = request, TrialNo = 2,
                IsActive = true, SentDate = day.AddDays(2), DeliveredSampleQuantityKg = 0, Status = SampleTrialStatus.SampleSent });
        }
        store.Seed(nameof(db.SampleRequestSampleTrials), trials.ToArray());
        var prices = new[] { new ProductPricingVersion { CompanyId = request.CompanyId, ProductId = request.ProductId,
            SourceFormulaId = formula.FormulaId, IsActive = true, Currency = "VND", Status = ProductPricingStatus.Approved,
            StandardSellingPrice = 100, ApprovedAt = redelivered ? day.AddDays(2) : day } }.AsQueryable();
        var records = SampleRequestAdminReportQuery.Build(new[] { request }.AsQueryable(), db, prices);
        var completed = SampleRequestAdminReportQuery.Filter(records, SampleRequestAdminMetric.Completed, day.AddMonths(-1), day.AddMonths(1)).Count();
        var working = SampleRequestAdminReportQuery.Filter(records, SampleRequestAdminMetric.InProgress, day.AddMonths(-1), day.AddMonths(1)).Count();
        Assert.Equal(expected ? 1 : 0, completed);
        if (expected)
        {
            Assert.Equal(quotation ? "quotation" : "chip", records.Single().LabCompletedKind);
            Assert.Single(SampleRequestAdminReportQuery.Filter(records,
                quotation ? SampleRequestAdminMetric.CompletedQuotation : SampleRequestAdminMetric.CompletedChip, day.AddMonths(-1), day.AddMonths(1)));
        }
        Assert.Equal(expected ? 0 : 1, working);
        Assert.Equal("InProgress", request.Status);
    }

    [Fact]
    public void CustomerPriceChipAndSampleAcceptanceAreIndependentAndUseLatestResultPerKind()
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<IPLMReadDbContext>();
        var day = new DateTime(2026, 11, 5);
        var request = new SampleRequest { SampleRequestId = Guid.NewGuid(), CompanyId = Guid.NewGuid(), CreatedDate = day, Status = "InProgress" };
        store.Seed<Formula>(nameof(db.Formulas));
        store.Seed(nameof(db.SampleRequestWorkflowEvents), new SampleRequestWorkflowEvent { CompanyId = request.CompanyId,
            SampleRequestId = request.SampleRequestId, EventType = SampleRequestWorkflowEvents.PriceAccepted, RecordedAt = day });
        store.Seed(nameof(db.SampleRequestSampleTrials),
            new SampleRequestSampleTrial { SampleRequestId = request.SampleRequestId, SampleRequest = request, IsActive = true,
                TrialNo = 1, DeliveredSampleQuantityKg = 0, Status = SampleTrialStatus.Approved, CustomerReplyDate = day },
            new SampleRequestSampleTrial { SampleRequestId = request.SampleRequestId, SampleRequest = request, IsActive = true,
                TrialNo = 2, DeliveredSampleQuantityKg = 2, Status = SampleTrialStatus.Approved, CustomerReplyDate = day });
        var records = SampleRequestAdminReportQuery.Build(new[] { request }.AsQueryable(), db);
        foreach (var metric in new[] { SampleRequestAdminMetric.PriceAccepted, SampleRequestAdminMetric.ChipAccepted, SampleRequestAdminMetric.SampleAccepted })
            Assert.Single(SampleRequestAdminReportQuery.Filter(records, metric, day.AddDays(-1), day.AddDays(1)));
        store.Seed(nameof(db.SampleRequestWorkflowEvents), new SampleRequestWorkflowEvent { CompanyId = request.CompanyId,
            SampleRequestId = request.SampleRequestId, EventType = SampleFlowRules.PriceRejected, RecordedAt = day.AddHours(1) });
        Assert.Empty(SampleRequestAdminReportQuery.Filter(SampleRequestAdminReportQuery.Build(new[] { request }.AsQueryable(), db),
            SampleRequestAdminMetric.PriceAccepted, day.AddDays(-1), day.AddDays(1)));
    }
}
