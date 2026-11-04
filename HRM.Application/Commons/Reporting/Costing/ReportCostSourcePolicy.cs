namespace HRM.Application.Commons.Reporting.Costing;

/// <summary>
/// Công tắc nội bộ nguồn giá vốn Executive PnL. Không nhận từ query/API để tránh mỗi người dùng xem một công thức khác nhau.
/// </summary>
internal static class ReportCostSourcePolicy
{
    // Đổi duy nhất dòng này khi cần chuyển nguồn tính giá vốn toàn hệ thống.
    //public static ReportCostSource Current => ReportCostSource.FormulaSnapshot;
    public static ReportCostSource Current => ReportCostSource.InventoryWeightedAverage;
}

internal enum ReportCostSource
{
    FormulaSnapshot = 1,
    WarehouseActual = 2,
    InventoryWeightedAverage = 3
}
