namespace HRM.Application.Features.Dispatch.DeliveryOrders.Dtos;

public sealed class DeliveryOrderLineDto
{
    public Guid Id { get; init; }
    public Guid? MerchandiseOrderDetailId { get; init; }
    public Guid? ProductId { get; init; }
    public string? ProductExternalId { get; init; }
    public string? ProductName { get; init; }
    public string? LotNoList { get; init; }
    public IReadOnlyList<DeliveryOrderLotDto> Lots { get; init; } = Array.Empty<DeliveryOrderLotDto>();
    public string? PONo { get; init; }
    public decimal Quantity { get; init; }
    public int NumOfBags { get; init; }
    public bool IsAttach { get; init; }
}
