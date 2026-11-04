using HRM.Domain.Entities.OrderSchema;
using Shared.Enums;

namespace HRM.Application.Features.Purchasing.PurchaseOrders.Services;

internal static class PurchaseOrderEditRules
{
    public const string LockedMessage = "Đơn mua hàng chỉ được sửa khi còn active và ở trạng thái Pending. Hãy tải lại dữ liệu.";
    public static bool CanEdit(PurchaseOrder order) => order.IsActive == true &&
        string.Equals(order.Status, PurchaseOrderStatus.Pending, StringComparison.OrdinalIgnoreCase);
    public static bool CanEditInternalNote(PurchaseOrder order) => order.IsActive == true &&
        (CanEdit(order) || string.Equals(order.Status, PurchaseOrderStatus.InProgress, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(order.Status, PurchaseOrderStatus.Completed, StringComparison.OrdinalIgnoreCase));
    public static bool ValidOrderType(string? value) => value?.Trim() is "NCC" or "KH" or "NB";
}
