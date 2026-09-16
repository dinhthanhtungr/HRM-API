using HRM.Domain.Enums.Boms;

namespace HRM.Application.Features.PLM.Boms.Dtos;

public sealed class ProductionOrderLossDto
{
    public Guid MfgProductionOrderLossId { get; init; }
    public Guid? SourceManufacturingBomLossRuleId { get; init; }
    public string LossTypeCode { get; init; } = string.Empty;
    public string LossTypeName { get; init; } = string.Empty;
    public LossCalculationMethod CalculationMethod { get; init; }
    public string? StageCode { get; init; }
    public string? MaterialCode { get; init; }
    public decimal PlannedQuantityKg { get; init; }
    public decimal? ActualQuantityKg { get; init; }
    public int? EventCount { get; init; }
    public decimal RecoveredQuantityKg { get; init; }
    public bool IncludeInMaterialRequest { get; init; }
    public bool IsFinalized { get; init; }
    public string? Note { get; init; }
}

public sealed class ProductionMaterialRequirementDto
{
    public Guid? MaterialId { get; init; }
    public string MaterialCode { get; init; } = string.Empty;
    public string? MaterialName { get; init; }
    public decimal BaseQuantityKg { get; init; }
    public decimal PlannedLossQuantityKg { get; init; }
    public decimal TotalRequiredQuantityKg { get; init; }
}

public sealed class PatchProductionOrderLossRequest
{
    public decimal? ActualQuantityKg { get; init; }
    public int? EventCount { get; init; }
    public decimal? RecoveredQuantityKg { get; init; }
    public string? Note { get; init; }
}
