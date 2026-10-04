using HRM.Domain.Enums.Boms;

namespace HRM.Application.Features.PLM.Boms.Dtos;

public sealed class CreateManufacturingBomRequest
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public decimal BaseOutputQuantity { get; init; }
    public string OutputUnit { get; init; } = "kg";
    public DateTime? EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
    public string? Note { get; init; }
}

public sealed class ReplaceManufacturingBomRequest
{
    public decimal BaseOutputQuantity { get; init; }
    public string OutputUnit { get; init; } = "kg";
    public DateTime? EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
    public string? ChangeReason { get; init; }
    public string? Note { get; init; }
    public IReadOnlyList<BomItemWriteDto> Items { get; init; } = [];
    public IReadOnlyList<ManufacturingBomStageWriteDto> Stages { get; init; } = [];
    public IReadOnlyList<ManufacturingBomLossRuleWriteDto> LossRules { get; init; } = [];
}

/// <summary>
/// Process-only configuration of a Formula-driven M-BOM. Material item identity, quantity and unit remain immutable.
/// </summary>
public sealed class UpdateManufacturingBomProcessConfigurationRequest
{
    public decimal BaseOutputQuantity { get; init; }
    public string OutputUnit { get; init; } = "kg";
    public DateTime? EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
    public string? ChangeReason { get; init; }
    public string? Note { get; init; }
    public IReadOnlyList<ManufacturingBomItemStageAssignmentDto> ItemStageAssignments { get; init; } = [];
    public IReadOnlyList<ManufacturingBomStageWriteDto> Stages { get; init; } = [];
    public IReadOnlyList<ManufacturingBomLossRuleWriteDto> LossRules { get; init; } = [];
}

public sealed class ManufacturingBomItemStageAssignmentDto
{
    public Guid BomVersionItemId { get; init; }
    public string ManufacturingStageCode { get; init; } = string.Empty;
}

/// <summary>
/// Replaces the operating values of every machine parameter in one Manufacturing BOM Draft.
/// Parameter identity and definition come from the applied process-template snapshot.
/// </summary>
public sealed class UpdateManufacturingBomMachineParametersRequest
{
    public IReadOnlyList<ManufacturingBomMachineParameterValueWriteDto> Parameters { get; init; } = [];
}

public sealed class ManufacturingBomMachineParameterValueWriteDto
{
    public Guid ManufacturingBomStageMachineParameterId { get; init; }
    public decimal? TargetValue { get; init; }
    public decimal? MinValue { get; init; }
    public decimal? MaxValue { get; init; }
    public string? Note { get; init; }
}

public sealed class ManufacturingBomStageWriteDto
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int SequenceNo { get; init; }
    public string? Description { get; init; }
}

public sealed class ManufacturingBomLossRuleWriteDto
{
    public Guid ManufacturingLossTypeId { get; init; }
    public int? ItemLineNo { get; init; }
    public string? StageCode { get; init; }
    public LossCalculationMethod CalculationMethod { get; init; }
    public decimal? RatePercent { get; init; }
    public decimal? FixedQuantityKg { get; init; }
    public decimal? QuantityPerEventKg { get; init; }
    public int? DefaultEventCount { get; init; }
    public int SequenceNo { get; init; }
    public bool IsRecoverable { get; init; }
    public bool IncludeInMaterialRequest { get; init; }
    public string? Note { get; init; }
}
