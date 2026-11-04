using HRM.Domain.Enums.Devandqa;
namespace HRM.Application.Features.Reports.PLPU;

public partial class PLPUPurchaseOverviewReportService
{
        private static List<string> ResolveHeaderAlertFlags(
            List<PLPUPurchaseOrderDetailReportDto> details)
        {
            return details
                .SelectMany(x => SplitFlags(x.AlertFlags))
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// Xác định trạng thái chính của header PO.
        /// </summary>
        private static string ResolveHeaderPrimaryStatus(
            bool isActive,
            string poStatus,
            List<PLPUPurchaseOrderDetailReportDto> details,
            decimal ordered,
            decimal received,
            decimal accepted,
            decimal pendingQc,
            decimal rejected)
        {
            if (!isActive || IsCancelledOrInactiveStatus(poStatus))
                return "Cancelled";

            if (IsClosedStatus(poStatus))
            {
                if (ordered > 0 && !IsQuantityEnough(accepted, ordered))
                    return "ClosedWithShortage";

                return "Closed";
            }

            if (ordered <= 0 || details.Count == 0)
                return "NoQuantity";

            if (IsQuantityEnough(accepted, ordered))
                return "Completed";

            if (details.Any(x => HasFlag(x.AlertFlags, "Overdue")))
                return "OverdueWithShortage";

            if (rejected > 0)
                return "HasQCFail";

            if (pendingQc > 0 && IsQuantityEnough(received, ordered))
                return "DeliveredEnoughWaitingQC";

            if (pendingQc > 0)
                return "WaitingQC";

            if (IsQuantityEnough(received, ordered))
                return "DeliveredEnough";

            if (accepted > 0)
                return "PartiallyAccepted";

            if (received > 0)
                return "PartiallyDelivered";

            return "NotDelivered";
        }

        /// <summary>
        /// Kiểm tra PO có cần buyer xử lý không.
        /// </summary>
        private static bool IsNeedActionHeader(List<string> flags, string reportStatus)
        {
            if (reportStatus is "Cancelled" or "Closed" or "Completed")
                return false;

            return flags.Contains("Overdue") ||
                   flags.Contains("Shortage") ||
                   flags.Contains("HasQCFail") ||
                   flags.Contains("WaitingQC") ||
                   flags.Contains("OverDelivered") ||
                   reportStatus == "ClosedWithShortage";
        }

        /// <summary>
        /// Kiểm tra dòng PO có cần buyer xử lý không.
        /// </summary>
        private static bool IsNeedActionLine(List<string> flags)
        {
            return flags.Contains("Overdue") ||
                   flags.Contains("Shortage") ||
                   flags.Contains("HasQCFail") ||
                   flags.Contains("WaitingQC") ||
                   flags.Contains("OverDelivered");
        }

        /// <summary>
        /// Đề xuất hành động buyer cần làm dựa trên cảnh báo.
        /// </summary>
        private static string ResolveBuyerAction(IEnumerable<string> flags)
        {
            var flagSet = flags.ToHashSet(StringComparer.OrdinalIgnoreCase);

            var actions = new List<string>();

            if (flagSet.Contains("Overdue") || flagSet.Contains("Shortage"))
                actions.Add("Follow up supplier for overdue shortage");

            if (flagSet.Contains("HasQCFail"))
                actions.Add("Handle QC fail with supplier");

            if (flagSet.Contains("WaitingQC"))
                actions.Add("Follow up QC decision");

            if (flagSet.Contains("OverDelivered"))
                actions.Add("Review over delivery");

            return string.Join("; ", actions);
        }

}
