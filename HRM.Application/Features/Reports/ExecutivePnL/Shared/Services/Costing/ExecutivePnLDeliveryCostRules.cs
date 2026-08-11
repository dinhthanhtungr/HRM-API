namespace HRM.Application.Features.Reports.ExecutivePnL.Shared.Services.Costing;

/// <summary>
/// Ưu tiên cost snapshot của các lot đã chuẩn hóa; legacy amount chỉ dùng khi detail chưa có lot consumption active.
/// </summary>
internal static class ExecutivePnLDeliveryCostRules
{
    public static decimal ResolveAmount(
        bool hasNormalizedLots,
        decimal lotCostSnapshotAmount,
        decimal legacyCostAmount)
        => hasNormalizedLots ? lotCostSnapshotAmount : legacyCostAmount;
}
