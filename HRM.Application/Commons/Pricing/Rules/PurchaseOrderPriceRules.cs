using Shared.Enums;

namespace HRM.Application.Commons.Pricing.Rules;

/// <summary>
/// Quy tắc chung khi đọc Purchase Order làm nguồn giá hoặc lịch sử mua NVL.
/// </summary>
public static class PurchaseOrderPriceRules
{
    public static readonly string[] CanceledStatuses =
    [
        PurchaseOrderStatus.Canceled,
        "Cancelled"
    ];
}
