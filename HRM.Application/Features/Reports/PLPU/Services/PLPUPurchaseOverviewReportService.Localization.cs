using HRM.Domain.Enums.Devandqa;
namespace HRM.Application.Features.Reports.PLPU;

public partial class PLPUPurchaseOverviewReportService
{
        private static string ToVietnameseStatus(string? status)
        {
            if (status != null && PLPUReportStatuses.Labels.TryGetValue(status, out var label)) return label;
            return status switch
            {
                "Cancelled" => "Đã hủy",
                "Closed" => "Đã đóng",
                "ClosedWithShortage" => "Đã đóng còn thiếu",
                "NoQuantity" => "Không có số lượng",
                "Completed" => "Hoàn thành",
                "AcceptedEnough" => "QC đạt đủ",
                "DeliveredEnoughWaitingQC" => "Đã giao đủ, chờ QC",
                "DeliveredEnough" => "Đã giao đủ",
                "OverdueWithShortage" => "Quá hạn còn thiếu",
                "HasQCFail" => "Có hàng lỗi QC",
                "WaitingQC" => "Chờ QC",
                "PartiallyAccepted" => "QC đạt một phần",
                "PartiallyDelivered" => "Giao một phần",
                "OverdueNotDelivered" => "Quá hạn chưa giao",
                "NotDelivered" => "Chưa giao",
                _ => status ?? string.Empty
            };
        }

        private static string ToVietnameseFlag(string? flag)
        {
            return flag switch
            {
                "Overdue" => "Quá hạn",
                "Shortage" => "Thiếu hàng",
                "HasQCFail" => "Có hàng lỗi QC",
                "WaitingQC" => "Chờ QC",
                "OverDelivered" => "Giao vượt",
                _ => flag ?? string.Empty
            };
        }

        private static string ToVietnameseFlags(string? flags)
        {
            if (string.IsNullOrWhiteSpace(flags))
                return string.Empty;

            return string.Join(", ",
                flags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                     .Select(ToVietnameseFlag));
        }

        private static string ToVietnameseBuyerAction(string? action)
        {
            if (string.IsNullOrWhiteSpace(action))
                return string.Empty;

            var parts = action.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var translated = parts.Select(x => x switch
            {
                "Follow up supplier for overdue shortage" => "Theo dõi NCC do quá hạn/thiếu hàng",
                "Handle QC fail with supplier" => "Xử lý hàng lỗi QC với NCC",
                "Follow up QC decision" => "Theo dõi kết quả QC",
                "Review over delivery" => "Kiểm tra giao vượt",
                _ => x
            });

            return string.Join("; ", translated);
        }

        private static string ToVietnameseQcGroup(string? qcGroup)
        {
            return qcGroup switch
            {
                "Accepted" => "Đạt QC",
                "Rejected" => "Không đạt QC",
                "Pending" => "Chờ QC",
                _ => qcGroup ?? string.Empty
            };
        }

        private static List<PLPUPurchaseOrderHeaderReportDto> LocalizeHeaders(
            IEnumerable<PLPUPurchaseOrderHeaderReportDto> headers)
        {
            return headers.Select(x =>
            {
                x.ReportStatus = ToVietnameseStatus(x.ReportStatus);
                x.AlertFlags = ToVietnameseFlags(x.AlertFlags);
                x.BuyerAction = ToVietnameseBuyerAction(x.BuyerAction);
                return x;
            }).ToList();
        }

        private static List<PLPUPurchaseOrderDetailReportDto> LocalizeDetails(
            IEnumerable<PLPUPurchaseOrderDetailReportDto> details)
        {
            return details.Select(x =>
            {
                x.LineReportStatus = ToVietnameseStatus(x.LineReportStatus);
                x.AlertFlags = ToVietnameseFlags(x.AlertFlags);
                x.BuyerAction = ToVietnameseBuyerAction(x.BuyerAction);
                return x;
            }).ToList();
        }

        private static List<PLPUPurchaseReceiptReportDto> LocalizeReceipts(
            IEnumerable<PLPUPurchaseReceiptReportDto> receipts)
        {
            return receipts.Select(x =>
            {
                x.QCGroup = ToVietnameseQcGroup(x.QCGroup);
                return x;
            }).ToList();
        }

        private static List<PLPUPurchaseQcReportDto> LocalizeQcRows(
            IEnumerable<PLPUPurchaseQcReportDto> rows)
        {
            return rows.Select(x =>
            {
                x.QCGroup = ToVietnameseQcGroup(x.QCGroup);
                x.BuyerAction = ToVietnameseBuyerAction(x.BuyerAction);
                return x;
            }).ToList();
        }

}
