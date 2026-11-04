namespace HRM.Application.Features.Reports.PLPU;

/// <summary>Codes returned by ResolveHeaderPrimaryStatus and grouped by BuildStatusChart.</summary>
public static class PLPUReportStatuses
{
    public static IReadOnlyDictionary<string, string> Labels { get; } =
        new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Cancelled"] = "Đã hủy",
            ["Closed"] = "Đã đóng",
            ["ClosedWithShortage"] = "Đã đóng còn thiếu",
            ["NoQuantity"] = "Không có số lượng",
            ["Completed"] = "Hoàn thành",
            ["OverdueWithShortage"] = "Quá hạn còn thiếu",
            ["HasQCFail"] = "Có hàng lỗi QC",
            ["DeliveredEnoughWaitingQC"] = "Đã giao đủ, chờ QC",
            ["WaitingQC"] = "Chờ QC",
            ["DeliveredEnough"] = "Đã giao đủ",
            ["PartiallyAccepted"] = "QC đạt một phần",
            ["PartiallyDelivered"] = "Giao một phần",
            ["NotDelivered"] = "Chưa giao"
        });
}
