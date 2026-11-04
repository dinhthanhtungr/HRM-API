using HRM.Application.Features.PLM.ProductionOrders.Dtos;
using HRM.Domain.Enums.Formulas;

namespace HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;

public sealed record ProductionOrderFormulaSourceItemDto(
    Guid SourceItemId,
    Guid ItemId,
    ItemType ItemType,
    Guid CategoryId,
    int LineNo,
    decimal Quantity,
    decimal? UnitPrice,
    string? Name,
    string? Code,
    string? Unit,
    string? LotNo
)
{
    public string? StockCode { get; init; }
    public DateTime? PriceDate { get; init; }
    public decimal OnHandKg { get; init; }
    public decimal ReservedOpenAllKg { get; init; }
    public decimal AvailableKg { get; init; }
    public IReadOnlyList<ProductionOrderLotNumberDto> Lots { get; init; } = [];
}

public sealed record ProductionOrderFormulaSourceDto(
    Guid FormulaId,
    string ExternalId,
    string? Note,
    bool PricesVisible,
    IReadOnlyList<ProductionOrderFormulaSourceItemDto> Items
)
{
    public HRM.Domain.Enums.Manufacturings.ManufacturingFormulaOrigin? Origin { get; init; }
    public DateTime? CreatedDate { get; init; }
    public string? Status { get; init; }
}
