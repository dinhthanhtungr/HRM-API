namespace HRM.Application.Features.PLM.SaleOrders.Dtos;

/// <summary>
/// Tồn thành phẩm của một product có thể truy vết về MFG của customer qua VA lot.
/// </summary>
public sealed class CustomerProductStockSummaryDto
{
    public Guid CustomerId { get; init; }
    public Guid ProductId { get; init; }
    public string ProductCode { get; init; } = string.Empty;
    public DateTime AsOf { get; init; }
    public decimal TotalOnHandKg { get; init; }
    public decimal ReservedOpenKg { get; init; }
    public decimal AvailableKg { get; init; }
    public decimal ProductAvailableKg { get; init; }
    public decimal AmbiguousOnHandKg { get; init; }
    public bool HasAmbiguousAttribution { get; init; }
    public IReadOnlyList<CustomerProductStockLotDto> Lots { get; init; } = [];
}

public sealed class CustomerProductStockLotDto
{
    public Guid ManufacturingFormulaId { get; init; }
    public string LotNo { get; init; } = string.Empty;
    public decimal OnHandKg { get; init; }
    public decimal ReservedOpenKg { get; init; }
    public decimal AvailableKg { get; init; }
    public bool IsAttributionAmbiguous { get; init; }
    public IReadOnlyList<CustomerProductStockMfgDto> SourceMfgOrders { get; init; } = [];
    public IReadOnlyList<CustomerProductStockShelfDto> Shelves { get; init; } = [];
}

public sealed class CustomerProductStockMfgDto
{
    public Guid MfgProductionOrderId { get; init; }
    public string ExternalId { get; init; } = string.Empty;
}

public sealed class CustomerProductStockShelfDto
{
    public string ShelfCode { get; init; } = string.Empty;
    public decimal OnHandKg { get; init; }
}
