namespace HRM.Application.Features.Dispatch.DeliveryOrders.Dtos;

public sealed class DeliveryOrderLotRequest
{
    public string? LotNo { get; init; }
    public decimal Quantity { get; init; }
}
