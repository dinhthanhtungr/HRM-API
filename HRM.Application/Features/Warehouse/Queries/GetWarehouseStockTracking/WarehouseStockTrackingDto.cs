using HRM.Domain.Enums.WareHouses;

namespace HRM.Application.Features.Warehouse.Queries.GetWarehouseStockTracking;

public sealed class WarehouseStockTrackingDto
{
    public required string Code { get; init; }
    public string? Name { get; init; }
    public StockType StockType { get; init; }
    public decimal OnHandKg { get; init; }
    public decimal? AvailableKg { get; init; }
}
