namespace HRM.Application.Commons.Reporting.Costing;

/// <summary>
/// Ưu tiên cost snapshot hợp lệ của các lot đã chuẩn hóa; nếu snapshot lịch sử bằng 0 thì dùng nguồn fallback.
/// </summary>
internal static class DeliveryCostRules
{
    public static decimal ResolveAmount(
        bool hasNormalizedLots,
        decimal lotCostSnapshotAmount,
        decimal legacyCostAmount)
        => hasNormalizedLots && lotCostSnapshotAmount > 0m
            ? lotCostSnapshotAmount
            : legacyCostAmount;
}
