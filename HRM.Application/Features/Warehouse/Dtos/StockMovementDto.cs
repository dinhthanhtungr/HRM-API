namespace HRM.Application.Features.Warehouse.Dtos;

public sealed record StockMovementDto(
    string MovementKey, DateTime MovementAt, bool IsGrouped,
    WarehouseVoucherBusinessTypeDto BusinessType, int LedgerCount,
    decimal InKg, decimal OutKg, decimal NetKg,
    IReadOnlyList<string> ProductCodes, IReadOnlyList<string> FromShelves,
    IReadOnlyList<string> ToShelves)
{
    public IReadOnlyList<StockMovementGoodsDto> Goods { get; init; } = [];
    public decimal? BeforeKg { get; init; }
    public decimal? AfterKg { get; init; }
    public IReadOnlyList<string> CreatedByNames { get; init; } = [];
    public IReadOnlyList<string> ReferenceCodes { get; init; } = [];
}

public sealed record StockMovementGoodsDto(string? ProductCode, string? LotNumber, int? StockType);

public sealed record StockMovementLineDto(
    long LedgerId, DateTime CreatedAt, string ShelfCode, string Scope,
    string? ProductCode, string? LotNumber, int? StockType, string? UnitName,
    decimal BeforeKg, decimal DeltaKg, decimal AfterKg,
    Guid? CreatedBy, string? CreatedByName, string? RequestCode, string? AppSource,
    long? VoucherId, string? VoucherCode);

public sealed record StockMovementDetailDto(StockMovementDto Movement, IReadOnlyList<StockMovementLineDto> Lines);
