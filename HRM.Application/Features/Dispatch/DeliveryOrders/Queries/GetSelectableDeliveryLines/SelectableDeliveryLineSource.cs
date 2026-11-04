using HRM.Domain.Entities.OrderSchema;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Queries.GetSelectableDeliveryLines;

internal sealed class SelectableDeliveryLineSource
{
    public MerchandiseOrder Order { get; init; } = null!;
    public MerchandiseOrderDetail Detail { get; init; } = null!;
    public decimal DeliveredQuantity { get; init; }
    public decimal RemainingQuantity { get; init; }
}
