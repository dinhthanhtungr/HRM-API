using HRM.Domain.Enums.Boms;

namespace HRM.Application.Features.PLM.Boms.Dtos;

public sealed class ManufacturingBomStageDto
{
    public Guid ManufacturingBomStageId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int SequenceNo { get; init; }
    public string? Description { get; init; }
    public bool IsActive { get; init; }
    public IReadOnlyList<ManufacturingBomStageMachineDto> Machines { get; init; } = [];
    public ManufacturingBomStageWorkInstructionDto? WorkInstruction { get; init; }
}

public sealed class ManufacturingBomStageMachineDto
{
    public Guid ManufacturingBomStageMachineId { get; init; }
    public int EquipmentId { get; init; }
    public string EquipmentExternalId { get; init; } = string.Empty;
    public string EquipmentName { get; init; } = string.Empty;
    public bool IsDefault { get; init; }
    public int SequenceNo { get; init; }
    public string? Note { get; init; }
    public IReadOnlyList<ManufacturingBomStageMachineParameterDto> Parameters { get; init; } = [];
}

public sealed class ManufacturingBomStageMachineParameterDto
{
    public Guid ManufacturingBomStageMachineParameterId { get; init; }
    public string ParameterCode { get; init; } = string.Empty;
    public string ParameterName { get; init; } = string.Empty;
    public decimal? TargetValue { get; init; }
    public decimal? MinValue { get; init; }
    public decimal? MaxValue { get; init; }
    public string Unit { get; init; } = string.Empty;
    public bool IsRequired { get; init; }
    public int SequenceNo { get; init; }
    public string? Note { get; init; }
}

public sealed class ManufacturingBomStageTransitionDto
{
    public Guid ManufacturingBomStageTransitionId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string FromStageCode { get; init; } = string.Empty;
    public string ToStageCode { get; init; } = string.Empty;
    public ManufacturingStageTransitionType TransitionType { get; init; }
    public int? DefaultEventCount { get; init; }
    public int SequenceNo { get; init; }
    public string? Note { get; init; }
}

public sealed class ManufacturingBomLossRuleDto
{
    public Guid ManufacturingBomLossRuleId { get; init; }
    public Guid ManufacturingLossTypeId { get; init; }
    public string LossTypeCode { get; init; } = string.Empty;
    public string LossTypeName { get; init; } = string.Empty;
    public int? ItemLineNo { get; init; }
    public string? StageCode { get; init; }
    public string? TransitionCode { get; init; }
    public ManufacturingLossScope Scope { get; init; }
    public ManufacturingLossAllocationMethod AllocationMethod { get; init; }
    public LossCalculationMethod CalculationMethod { get; init; }
    public decimal? RatePercent { get; init; }
    public decimal? FixedQuantityKg { get; init; }
    public decimal? QuantityPerEventKg { get; init; }
    public int? DefaultEventCount { get; init; }
    public int SequenceNo { get; init; }
    public bool IsRecoverable { get; init; }
    public bool IncludeInMaterialRequest { get; init; }
    public bool IsActive { get; init; }
    public string? Note { get; init; }
}
