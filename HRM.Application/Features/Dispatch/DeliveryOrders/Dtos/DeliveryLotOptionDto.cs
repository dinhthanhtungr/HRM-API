namespace HRM.Application.Features.Dispatch.DeliveryOrders.Dtos;

public sealed class DeliveryLotOptionDto
{
    public string? LotNo { get; init; }
    public string? LotKey { get; init; }
    public string StockType { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public int? Bags { get; init; }
}
