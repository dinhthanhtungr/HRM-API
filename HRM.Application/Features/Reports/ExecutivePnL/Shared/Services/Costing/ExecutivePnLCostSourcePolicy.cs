namespace HRM.Application.Features.Reports.ExecutivePnL.Shared.Services.Costing;

/// <summary>
/// Công tắc nội bộ nguồn giá vốn Executive PnL. Không nhận từ query/API để tránh mỗi người dùng xem một công thức khác nhau.
/// </summary>
internal static class ExecutivePnLCostSourcePolicy
{
    // Đổi duy nhất dòng này khi cần chuyển nguồn tính giá vốn toàn hệ thống.
    //public static ExecutivePnLCostSource Current => ExecutivePnLCostSource.FormulaSnapshot;
    public static ExecutivePnLCostSource Current => ExecutivePnLCostSource.InventoryWeightedAverage;
}

internal enum ExecutivePnLCostSource
{
    FormulaSnapshot = 1,
    WarehouseActual = 2,
    InventoryWeightedAverage = 3
}
