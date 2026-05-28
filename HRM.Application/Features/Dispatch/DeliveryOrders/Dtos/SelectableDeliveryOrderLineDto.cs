namespace HRM.Application.Features.Dispatch.DeliveryOrders.Dtos;

public sealed class SelectableDeliveryOrderLineDto
{
    public Guid MerchandiseOrderDetailId { get; init; }
    public Guid ProductId { get; init; }
    public string ProductExternalId { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public string? PONo { get; init; }
    public string? Note { get; init; }
    public decimal OrderedQuantity { get; init; }
    public decimal DeliveredQuantity { get; init; }
    public decimal RemainingQuantity { get; init; }
    public decimal AvailableStockQuantity { get; init; }
    public IReadOnlyList<DeliveryLotOptionDto> LotOptions { get; init; } = Array.Empty<DeliveryLotOptionDto>();
}
