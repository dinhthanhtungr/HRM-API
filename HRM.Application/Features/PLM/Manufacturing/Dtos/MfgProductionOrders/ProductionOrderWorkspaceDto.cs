using HRM.Domain.Enums.Manufacturings;

namespace HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;

public sealed record ProductionOrderFormulaOptionDto(
    Guid FormulaId,
    string ExternalId,
    ManufacturingFormulaOrigin Origin,
    Guid? SourceProductionOrderId,
    DateTime CreatedDate,
    bool RequiresFormulaReview
)
{
    public bool IsComplaintSource { get; init; }
    public string? Note { get; init; }
    public string? PlpuNote { get; init; }
    public string? LabNote { get; init; }
    public string? Requirement { get; init; }
}
public sealed record ManufacturingProductLotDto(
    string Code,
    string LotNo,
    decimal QuantityKg,
    int Bags,
    DateTime? ManufacturingDate,
    string Status
);
public sealed record ProductionOrderWorkspaceDto(
    GetMfgProductionOrder Order,
    IReadOnlyList<ProductionOrderFormulaOptionDto> FormulaOptions,
    bool CanUseStock,
    bool CanCreateFormula,
    int AdditionalQcBatchCount,
    decimal QcQuantityDeltaKg
)
{
    public IReadOnlyList<ManufacturingProductLotDto> ProductLots { get; init; } = [];
    public IReadOnlyList<string> AllowedInformationActions { get; init; } = [];
}
