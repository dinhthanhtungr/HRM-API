using HRM.Domain.Enums.Manufacturings;

namespace HRM.Application.Features.PLM.ProductionOrders.Rules;

/// <summary>
/// Quy ước trạng thái của lệnh sản xuất dùng chung trong feature Production Orders.
/// </summary>
internal static class ProductionOrderStatusRules
{
    /// <summary>
    /// Các trạng thái thể hiện lệnh đã bắt đầu và hiện đang thực thi sản xuất.
    /// </summary>
    public static readonly string[] InProductionStatuses =
    [
        ManufacturingProductOrder.Unassign.ToString(),
        ManufacturingProductOrder.Getback.ToString(),
        ManufacturingProductOrder.QCinprogress.ToString(),
        ManufacturingProductOrder.QCPassed.ToString(),
        ManufacturingProductOrder.QCFail.ToString(),
        ManufacturingProductOrder.BTPNew.ToString(),
        ManufacturingProductOrder.Weighting.ToString(),
        ManufacturingProductOrder.Weighted.ToString(),
        ManufacturingProductOrder.Mixing.ToString(),
        ManufacturingProductOrder.Mixed.ToString(),
        ManufacturingProductOrder.Started.ToString(),
        ManufacturingProductOrder.Canceled.ToString(),
        ManufacturingProductOrder.Running.ToString()
    ];
}
