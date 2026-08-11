using HRM.Application.Features.PLM.ComplaintReports.Queries;
using HRM.Domain.Enums.Orders;

namespace HRM.Application.Tests.Features.PLM.ComplaintReports;

public sealed class ComplaintReportReadRulesTests
{
    [Theory]
    [InlineData(100, 25, 75)]
    [InlineData(100, 100, 0)]
    [InlineData(100, 125, 0)]
    public void RemainingComplaintableQuantity_NeverReturnsNegative(
        decimal delivered,
        decimal complained,
        decimal expected)
    {
        var result = ComplaintReportReadRules.RemainingComplaintableQuantity(delivered, complained);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(ComplaintReportStatus.Draft, "Draft")]
    [InlineData(ComplaintReportStatus.Submitted, "InProgress")]
    [InlineData(ComplaintReportStatus.ActionInProgress, "InProgress")]
    [InlineData(ComplaintReportStatus.Closed, "Completed")]
    [InlineData(ComplaintReportStatus.Rejected, "Error")]
    [InlineData(ComplaintReportStatus.Cancelled, "Error")]
    public void TimelineBadge_MapsStatusForTimeline(
        ComplaintReportStatus status,
        string expected)
    {
        Assert.Equal(expected, ComplaintReportReadRules.TimelineBadge(status));
    }
}
