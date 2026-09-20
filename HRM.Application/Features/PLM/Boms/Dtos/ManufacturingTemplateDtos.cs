using HRM.Domain.Enums.Boms;

namespace HRM.Application.Features.PLM.Boms.Dtos;

public sealed class ManufacturingWorkInstructionChecklistWriteDto
{
    public Guid? ManufacturingWorkInstructionChecklistItemId { get; init; }
    public string Content { get; init; } = string.Empty;
    public int SequenceNo { get; init; }
    public bool IsRequired { get; init; }
    public string? ExpectedValue { get; init; }
    public string? Unit { get; init; }
}

public sealed class UpsertManufacturingWorkInstructionTemplateRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Purpose { get; init; }
    public string? Preparation { get; init; }
    public string Procedure { get; init; } = string.Empty;
    public string? QualityRequirements { get; init; }
    public string? SafetyNotes { get; init; }
    public DateTime? EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
    public IReadOnlyList<ManufacturingWorkInstructionChecklistWriteDto> ChecklistItems { get; init; } = [];
}

public sealed class ManufacturingWorkInstructionChecklistDto
{
    public Guid ManufacturingWorkInstructionChecklistItemId { get; init; }
    public string ExternalId { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public int SequenceNo { get; init; }
    public bool IsRequired { get; init; }
    public string? ExpectedValue { get; init; }
    public string? Unit { get; init; }
}

public sealed class ManufacturingWorkInstructionTemplateDto
{
    public Guid ManufacturingWorkInstructionTemplateId { get; init; }
    public string ExternalId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int VersionNo { get; init; }
    public ManufacturingTemplateStatus Status { get; init; }
    public string? Purpose { get; init; }
    public string? Preparation { get; init; }
    public string Procedure { get; init; } = string.Empty;
    public string? QualityRequirements { get; init; }
    public string? SafetyNotes { get; init; }
    public DateTime? EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
    public IReadOnlyList<ManufacturingWorkInstructionChecklistDto> ChecklistItems { get; init; } = [];
}

public sealed class ManufacturingProcessTemplateStageWriteDto
{
    public Guid? ManufacturingProcessTemplateStageId { get; init; }
    public string Name { get; init; } = string.Empty;
    public int SequenceNo { get; init; }
    public string? Description { get; init; }
    public Guid? ManufacturingWorkInstructionTemplateId { get; init; }
    public IReadOnlyList<ManufacturingProcessTemplateStageMachineWriteDto> Machines { get; init; } = [];
}

public sealed class ManufacturingProcessTemplateStageMachineWriteDto
{
    public int EquipmentId { get; init; }
    public bool IsDefault { get; init; }
    public int SequenceNo { get; init; }
    public string? Note { get; init; }
}

public sealed class UpsertManufacturingProcessTemplateRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public DateTime? EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
    public IReadOnlyList<ManufacturingProcessTemplateStageWriteDto> Stages { get; init; } = [];
}

public sealed class ManufacturingProcessTemplateStageDto
{
    public Guid ManufacturingProcessTemplateStageId { get; init; }
    public string ExternalId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int SequenceNo { get; init; }
    public string? Description { get; init; }
    public Guid? ManufacturingWorkInstructionTemplateId { get; init; }
    public string? WorkInstructionExternalId { get; init; }
    public string? WorkInstructionName { get; init; }
    public int? WorkInstructionVersionNo { get; init; }
    public IReadOnlyList<ManufacturingProcessTemplateStageMachineDto> Machines { get; init; } = [];
}

public sealed class ManufacturingProcessTemplateStageMachineDto
{
    public int EquipmentId { get; init; }
    public string EquipmentExternalId { get; init; } = string.Empty;
    public string EquipmentName { get; init; } = string.Empty;
    public bool IsDefault { get; init; }
    public int SequenceNo { get; init; }
    public string? Note { get; init; }
}

public sealed class ManufacturingProcessTemplateDto
{
    public Guid ManufacturingProcessTemplateId { get; init; }
    public string ExternalId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int VersionNo { get; init; }
    public ManufacturingTemplateStatus Status { get; init; }
    public DateTime? EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
    public IReadOnlyList<ManufacturingProcessTemplateStageDto> Stages { get; init; } = [];
}

public enum ManufacturingTemplateLifecycleAction
{
    Release,
    Obsolete
}

public sealed class ApplyManufacturingProcessTemplateRequest
{
    public Guid ProcessTemplateId { get; init; }
}

public sealed class ManufacturingProcessApplicationStageDto
{
    public string ExternalId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int SequenceNo { get; init; }
    public ManufacturingBomStageWorkInstructionDto? WorkInstruction { get; init; }
    public IReadOnlyList<ManufacturingBomStageMachineDto> Machines { get; init; } = [];
}

public sealed class ManufacturingProcessTemplateApplicationDto
{
    public Guid BomVersionId { get; init; }
    public Guid ProcessTemplateId { get; init; }
    public string ProcessTemplateExternalId { get; init; } = string.Empty;
    public int ProcessTemplateVersionNo { get; init; }
    public bool IsPreview { get; init; }
    public bool CanApply { get; init; }
    public IReadOnlyList<ManufacturingProcessApplicationIssueDto> BlockingReasons { get; init; } = [];
    public IReadOnlyList<ManufacturingProcessApplicationIssueDto> Warnings { get; init; } = [];
    public IReadOnlyList<ManufacturingProcessApplicationStageDto> Stages { get; init; } = [];
}

public sealed class ManufacturingProcessApplicationIssueDto
{
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}

public sealed class ManufacturingBomStageWorkInstructionDto
{
    public Guid? ManufacturingBomStageWorkInstructionId { get; init; }
    public Guid? SourceWorkInstructionTemplateId { get; init; }
    public string ExternalIdSnapshot { get; init; } = string.Empty;
    public string NameSnapshot { get; init; } = string.Empty;
    public int VersionNoSnapshot { get; init; }
    public string? PurposeSnapshot { get; init; }
    public string? PreparationSnapshot { get; init; }
    public string ProcedureSnapshot { get; init; } = string.Empty;
    public string? QualityRequirementsSnapshot { get; init; }
    public string? SafetyNotesSnapshot { get; init; }
    public IReadOnlyList<ManufacturingBomStageChecklistItemDto> ChecklistItems { get; init; } = [];
}

public sealed class ManufacturingBomStageChecklistItemDto
{
    public Guid? ManufacturingBomStageChecklistItemId { get; init; }
    public string ExternalIdSnapshot { get; init; } = string.Empty;
    public string ContentSnapshot { get; init; } = string.Empty;
    public int SequenceNo { get; init; }
    public bool IsRequired { get; init; }
    public string? ExpectedValueSnapshot { get; init; }
    public string? UnitSnapshot { get; init; }
}

public sealed class CreateManufacturingTemplateVersionRequest
{
    public string? ChangeReason { get; init; }
}

public sealed class ManufacturingTemplateOptionDto
{
    public Guid Id { get; init; }
    public string ExternalId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int VersionNo { get; init; }
    public int? StageCount { get; init; }
    public string DisplayName { get; init; } = string.Empty;
}
