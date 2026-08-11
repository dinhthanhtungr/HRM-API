using HRM.Domain.Enums.Orders;

namespace HRM.Application.Features.PLM.ComplaintReports.Queries;

internal static class ComplaintReportReadRules
{
    internal static decimal RemainingComplaintableQuantity(decimal delivered, decimal complained)
        => Math.Max(0, delivered - complained);

    internal static string TimelineBadge(ComplaintReportStatus status)
        => status switch
        {
            ComplaintReportStatus.Closed => "Completed",
            ComplaintReportStatus.Rejected or ComplaintReportStatus.Cancelled => "Error",
            ComplaintReportStatus.Draft => "Draft",
            _ => "InProgress"
        };
}
