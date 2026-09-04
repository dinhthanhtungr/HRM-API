namespace HRM.Application.Features.CRM.Quotations.Services;

public sealed class QuotationFeatureOptions
{
    public const string SectionName = "Features:Quotations";

    public bool AllowSendingWithoutApprovedPricingVersion { get; set; }

    /// <summary>
    /// Số ngày một snapshot chi phí được xem là cũ. Đặt 0 để tắt cảnh báo này.
    /// </summary>
    public int CostingStaleAfterDays { get; set; } = 14;

    /// <summary>
    /// Số ngày sau khi President duyệt mà bảng giá phải được rà soát lại. Đặt 0 để tắt.
    /// </summary>
    public int ApprovedPricingReviewAfterDays { get; set; } = 30;

    /// <summary>
    /// Ngưỡng phần trăm chênh lệch giữa chi phí NVL realtime và snapshot để yêu cầu rà soát lại.
    /// Đặt 0 để tắt.
    /// </summary>
    public decimal MaterialCostChangeThresholdPercent { get; set; } = 5m;
}
