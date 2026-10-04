using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.SampleRequests.Dtos.SampleTrials;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;
using HRM.Domain.Enums.SampleRequests;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleRequestDailyWorkRulesTests
{
    private static readonly DateOnly ReportDate = new(2026, 9, 23);

    [Fact]
    public void LabProgressEditors_AreLabDeveloperAndPresident_NotSale()
    {
        var editors = ApplicationRoleSets.PLM.SampleRequestLabProgressEditors;
        Assert.Contains(ApplicationRoles.Lab.LabUser, editors);
        Assert.Contains(ApplicationRoles.Lab.LabAdmin, editors);
        Assert.Contains(ApplicationRoles.Developer, editors);
        Assert.Contains(ApplicationRoles.President, editors);
        Assert.DoesNotContain(ApplicationRoles.Sales.SaleUser, editors);
    }

    [Fact]
    public void DelayedFlag_DoesNotReplaceNextAction()
    {
        var item = new SampleRequestSampleTrialReportDto
        {
            LabReceivedDate = new DateTime(2026, 9, 20),
            ExpectedDeliveryDate = new DateTime(2026, 9, 21),
            Status = SampleTrialStatus.SampleSent,
            IsDelayed = true
        };

        Apply(item);

        Assert.Equal("AwaitingSaleReceipt", item.WorkStatus);
        Assert.Equal("HandleLabSample", item.NextActionCode);
        Assert.True(item.IsDelayed);
        Assert.True(item.IsOverdue);
    }

    [Fact]
    public void FailedTrial_RequiresRework_AndKeepsFeedbackSignal()
    {
        var item = new SampleRequestSampleTrialReportDto
        {
            Status = SampleTrialStatus.Failed,
            CustomerReplyDate = new DateTime(2026, 9, 23, 10, 0, 0),
            SampleRequestStatus = "InProgress"
        };

        Apply(item);

        Assert.Equal("ReworkRequired", item.WorkStatus);
        Assert.Equal("ReworkFormula", item.NextActionCode);
        Assert.True(item.FeedbackRecordedOnDate);
    }

    [Fact]
    public void Quotation_WithApprovedFormulaButNoPrice_WaitsForStandardPrice()
    {
        var item = new SampleRequestSampleTrialReportDto
        {
            InfoType = "Báo giá",
            LabReceivedDate = new DateTime(2026, 9, 20),
            FormulaStatus = "Approved",
            ExpectedPriceQuoteDate = new DateTime(2026, 9, 23)
        };

        Apply(item);

        Assert.Equal("AwaitingStandardPrice", item.WorkStatus);
        Assert.Equal("President", item.ResponsibleRole);
    }

    [Fact]
    public void Quotation_WithApprovedPrice_WaitsForSaleToQuote()
    {
        var item = new SampleRequestSampleTrialReportDto
        {
            InfoType = "Quotation",
            LabReceivedDate = new DateTime(2026, 9, 20),
            FormulaStatus = "Approved"
        };

        Apply(item, hasApprovedPrice: true);

        Assert.Equal("AwaitingQuotation", item.WorkStatus);
        Assert.Equal("SendQuotation", item.NextActionCode);
    }

    [Fact]
    public void SaleAction_IsMineOnlyForAssignedManager()
    {
        var saleId = Guid.NewGuid();
        var item = new SampleRequestSampleTrialReportDto
        {
            Status = SampleTrialStatus.SampleSent,
            ManagerSalesEmployeeId = saleId
        };

        Apply(item, canHandleSale: true, currentEmployeeId: saleId);
        Assert.True(item.IsActionForCurrentUser);

        Apply(item, canHandleSale: true, currentEmployeeId: Guid.NewGuid());
        Assert.False(item.IsActionForCurrentUser);
    }

    private static void Apply(
        SampleRequestSampleTrialReportDto item,
        bool hasApprovedPrice = false,
        bool canHandleSale = false,
        Guid? currentEmployeeId = null)
        => SampleRequestDailyWorkRules.Apply(
            item, ReportDate, hasApprovedPrice,
            canHandleLab: false, canApprovePrice: false,
            canHandleSale: canHandleSale, currentEmployeeId: currentEmployeeId);
}
