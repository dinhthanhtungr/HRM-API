using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Enums.Orders;

namespace HRM.Application.Features.PLM.ComplaintReports.Services;

public static class ComplaintDecisionRules
{
    public static bool CanMakeInitialDecision(ComplaintReportStatus status)
        => status == ComplaintReportStatus.Submitted;

    public static bool CanMakeFinalDecision(ComplaintReportStatus status)
        => status == ComplaintReportStatus.PendingFinalApproval;

    public static bool IsInitialDecisionSupported(ComplaintApprovalDecision decision)
        => decision is ComplaintApprovalDecision.Approved
            or ComplaintApprovalDecision.Rejected
            or ComplaintApprovalDecision.Returned;

    public static bool IsFinalDecisionSupported(ComplaintApprovalDecision decision)
        => decision is ComplaintApprovalDecision.Approved or ComplaintApprovalDecision.Returned;

    public static string? ValidateReplacementQuantities(
        IReadOnlyCollection<ComplaintReportLine> activeLines,
        IReadOnlyDictionary<Guid, decimal?> quantities)
    {
        if (activeLines.Count == 0 || quantities.Count != activeLines.Count)
        {
            return "Phải duyệt số lượng cho đúng toàn bộ dòng complaint active.";
        }

        foreach (var line in activeLines)
        {
            if (!quantities.TryGetValue(line.ComplaintReportLineId, out var quantity) ||
                !quantity.HasValue || quantity.Value <= 0 || quantity.Value > line.ComplaintQuantity)
            {
                return "Số lượng sản xuất bù phải lớn hơn 0 và không vượt số lượng complaint.";
            }
        }

        return null;
    }

    public static bool HasExactlyOneMfgPerDetail(
        IReadOnlyCollection<Guid> activeDetailIds,
        IReadOnlyCollection<Guid> linkedDetailIds)
    {
        if (activeDetailIds.Count == 0 || linkedDetailIds.Count != activeDetailIds.Count)
        {
            return false;
        }

        var expected = activeDetailIds.ToHashSet();
        return linkedDetailIds.All(expected.Contains) &&
            linkedDetailIds.GroupBy(x => x).All(x => x.Count() == 1);
    }
}
