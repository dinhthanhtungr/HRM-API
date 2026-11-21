using HRM.Application.Features.Warehouse.Dtos;

namespace HRM.Application.Features.DevAndQA.MaterialEquivalences;

public sealed record EquivalentMaterialDto(Guid MaterialId, string Code, string Name, Guid CategoryId, string? Unit);
public sealed record MaterialEquivalenceDto(
    Guid ProductId, string ProductCode, string ColourCode, string ProductName,
    string ProcessingType, Guid? FormulaId, string? FormulaCode, string MatchStatus,
    IReadOnlyList<EquivalentMaterialDto> Candidates,
    IReadOnlyList<StockAvailableDto> ProductStocks,
    IReadOnlyList<StockAvailableDto> MaterialStocks);
