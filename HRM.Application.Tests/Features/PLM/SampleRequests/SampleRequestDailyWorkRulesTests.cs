using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.SampleRequests.Dtos.SampleTrials;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;
using HRM.Domain.Enums.SampleRequests;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleRequestDailyWorkRulesTests
{
    private static readonly DateOnly ReportDate = new(2026, 9, 23);

    [Fact]
    public void MissingStandardReceiptDoesNotBlockFormulaOrSendingWork()
    {
        var item = new SampleRequestSampleTrialReportDto { InfoType = "Thẻ màu (1)", SampleRequestStatus = "InProgress" };
        Apply(item);
        Assert.Equal("AwaitingFormulaApproval", item.WorkStatus);
        item.FormulaStatus = "Approved";
        Apply(item);
        Assert.Equal("AwaitingLabSend", item.WorkStatus);
        Assert.Null(item.LabReceivedDate);
    }

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
    public void FinishedLabWorkWithManualDelay_RequiresReviewWithoutOverdue()
    {
        var item = new SampleRequestSampleTrialReportDto
        {
            LabReceivedDate = new DateTime(2026, 9, 20),
            ExpectedDeliveryDate = new DateTime(2026, 9, 21),
            Status = SampleTrialStatus.SampleSent,
            IsDelayed = true
        };

        Apply(item);

        Assert.Equal("ReviewDelayFlag", item.WorkStatus);
        Assert.Equal("ClearDelay", item.NextActionCode);
        Assert.True(item.IsDelayed);
        Assert.False(item.IsOverdue);
        Assert.True(item.IsLabWorkComplete);
        Assert.Equal("Manual", item.DelayReason);
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

        item.LabFormulaConfirmedAt = new DateTime(2026, 9, 22);
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

        item.LabFormulaConfirmedAt = new DateTime(2026, 9, 22);
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

    [Fact]
    public void ApprovedFormulaWithoutQuoteRequest_StillBelongsToLab()
    {
        var item = new SampleRequestSampleTrialReportDto
        {
            InfoType = "Quotation",
            LabReceivedDate = new DateTime(2026, 9, 20),
            FormulaStatus = "Approved"
        };

        SampleRequestDailyWorkRules.Apply(item, ReportDate, false, true, false, false, null);

        Assert.Equal("ApproveFormula", item.NextActionCode);
        Assert.Equal("Lab", item.ResponsibleRole);
        Assert.True(item.IsActionForCurrentUser);
    }

    [Fact]
    public void DueDate_UsesLabPlanThenSaleDateThenLabReceiptAndCalendarDays()
    {
        var item = new SampleRequestSampleTrialReportDto
        {
            RequestDeliveryDate = new DateTime(2026, 10, 31),
            LabReceivedDate = new DateTime(2026, 10, 30, 15, 0, 0),
            NumberDeliverySampleDate = 3,
            ExpectedDeliveryDate = new DateTime(2026, 11, 10)
        };
        Assert.Equal((item.ExpectedDeliveryDate, "LabExpected"), SampleRequestDailyWorkRules.ResolveDueDate(item));
        item.ExpectedDeliveryDate = null;
        Assert.Equal((item.RequestDeliveryDate, "SaleRequested"), SampleRequestDailyWorkRules.ResolveDueDate(item));
        item.RequestDeliveryDate = null;
        Assert.Equal(((DateTime?)new DateTime(2026, 11, 2), "LabReceiptPlusDays"), SampleRequestDailyWorkRules.ResolveDueDate(item));
        item.LabReceivedDate = null;
        Assert.Equal(((DateTime?)null, "Unknown"), SampleRequestDailyWorkRules.ResolveDueDate(item));
    }

    [Fact]
    public void ChangingPlanChangesDisplayedDueDateAndOverdueWithoutChangingSaleRequest()
    {
        var item = new SampleRequestSampleTrialReportDto
        {
            InfoType = "Thẻ màu", SampleRequestStatus = "InProgress", FormulaStatus = "Draft",
            RequestDeliveryDate = new DateTime(2026, 11, 3), ExpectedDeliveryDate = new DateTime(2026, 11, 6)
        };
        SampleRequestDailyWorkRules.Apply(item, new DateOnly(2026, 11, 4), false, true, false, false, null);
        Assert.Equal(new DateTime(2026, 11, 6), item.DueDate);
        Assert.Equal("LabExpected", item.DueDateSource);
        Assert.False(item.IsOverdue);
        Assert.Equal(new DateTime(2026, 11, 3), item.RequestDeliveryDate);
        item.ExpectedDeliveryDate = new DateTime(2026, 11, 2);
        SampleRequestDailyWorkRules.Apply(item, new DateOnly(2026, 11, 4), false, true, false, false, null);
        Assert.True(item.IsOverdue);
        Assert.Equal(new DateTime(2026, 11, 2), item.DueDate);
    }

    [Fact]
    public void FailedQuotation_ReopensUntilNewConfirmation()
    {
        var feedback = new DateTime(2026, 10, 30, 12, 0, 0);
        var item = new SampleRequestSampleTrialReportDto
        {
            InfoType = "Quotation", Status = SampleTrialStatus.Failed,
            CustomerReplyDate = feedback, LabFormulaConfirmedAt = feedback.AddDays(-1)
        };
        Assert.False(SampleRequestDailyWorkRules.IsLabComplete(item));
        item.LabFormulaConfirmedAt = feedback.AddMinutes(1);
        Assert.True(SampleRequestDailyWorkRules.IsLabComplete(item));
    }
}
