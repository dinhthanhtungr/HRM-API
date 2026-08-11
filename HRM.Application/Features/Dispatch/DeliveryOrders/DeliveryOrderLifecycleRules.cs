using HRM.Domain.Enums.Deliveries;

namespace HRM.Application.Features.Dispatch.DeliveryOrders;

internal static class DeliveryOrderLifecycleRules
{
    public static bool TryNormalizeStatus(
        string? value,
        out DeliveryOrderStatus status)
    {
        if (string.Equals(value?.Trim(), "Cancelled", StringComparison.OrdinalIgnoreCase))
        {
            status = DeliveryOrderStatus.Canceled;
            return true;
        }

        return Enum.TryParse(value?.Trim(), true, out status) &&
               Enum.IsDefined(status);
    }

    public static bool CanEditContent(string? currentStatus)
        => TryNormalizeStatus(currentStatus, out var status) &&
           status == DeliveryOrderStatus.Pending;

    public static bool CanTransition(
        DeliveryOrderStatus currentStatus,
        DeliveryOrderStatus targetStatus)
    {
        if (currentStatus == targetStatus)
        {
            return true;
        }

        return currentStatus switch
        {
            DeliveryOrderStatus.Pending => targetStatus is
                DeliveryOrderStatus.InProgress or DeliveryOrderStatus.Canceled,
            DeliveryOrderStatus.InProgress => targetStatus is
                DeliveryOrderStatus.Completed or DeliveryOrderStatus.Canceled,
            DeliveryOrderStatus.Completed or DeliveryOrderStatus.Canceled => false,
            _ => false
        };
    }
}
