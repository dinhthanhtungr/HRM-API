namespace HRM.Application.Features.Executive.MerchandiseOrderPriceHistory.Dtos;

/// <summary>Giá bán thực tế của một dòng Merchandise Order.</summary>
public sealed class MerchandiseOrderPriceHistoryItemDto
{
    public Guid Id { get; init; }
    public Guid MerchandiseOrderId { get; init; }
    public string MerchandiseOrderCode { get; init; } = string.Empty;
    public DateTime OrderedAt { get; init; }
    public string OrderType { get; init; } = string.Empty;
    public string OrderStatus { get; init; } = string.Empty;
    public Guid ItemId { get; init; }
    public string ItemCode { get; init; } = string.Empty;
    public string ItemName { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public string? Unit { get; init; }
    public decimal UnitPrice { get; init; }
    public string? Currency { get; init; }
    public Guid CustomerId { get; init; }
    public string CustomerCode { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public Guid SaleEmployeeId { get; init; }
    public string SaleName { get; init; } = string.Empty;
}

public sealed class LatestMerchandiseOrderDto
{
    public Guid MerchandiseOrderId { get; init; }
    public string MerchandiseOrderCode { get; init; } = string.Empty;
    public DateTime OrderedAt { get; init; }
    public string OrderType { get; init; } = string.Empty;
    public Guid ItemId { get; init; }
    public string ItemCode { get; init; } = string.Empty;
    public string ItemName { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public string? Unit { get; init; }
    public decimal UnitPrice { get; init; }
    public string? Currency { get; init; }
    public Guid CustomerId { get; init; }
    public string CustomerCode { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public Guid SaleEmployeeId { get; init; }
    public string SaleName { get; init; } = string.Empty;
    public string OrderStatus { get; init; } = string.Empty;
}
