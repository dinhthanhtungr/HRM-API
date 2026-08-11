using HRM.Application.Features.PLM.SaleOrders.Dtos;
using HRM.Domain.Enums.Merchadises;

namespace HRM.Application.Features.PLM.SaleOrders.Services;

/// <summary>
/// Các rule thuần dùng để suy ra trạng thái hiển thị của SaleOrder từ dữ liệu nghiệp vụ.
/// </summary>
internal static class SaleOrderStatusRules
{
    /// <summary>
    /// Hiển thị trạng thái Paused khi ngày hiện tại nằm trong thời gian tạm dừng giao hàng.
    /// Đơn đã hủy không bị ghi đè trạng thái.
    /// </summary>
    public static void ApplyEffectivePausedStatus(SaleOrderListItemDto item, DateTime now)
    {
        if (item.Status == MerchadiseStatus.Cancelled.ToString() || !item.IsDeliveryPaused)
        {
            return;
        }

        var activeFrom = !item.DeliveryPausedFrom.HasValue || item.DeliveryPausedFrom.Value.Date <= now.Date;
        var activeTo = !item.DeliveryPausedTo.HasValue || item.DeliveryPausedTo.Value.Date >= now.Date;
        if (activeFrom && activeTo)
        {
            item.Status = MerchadiseStatus.Paused.ToString();
        }
    }
}
