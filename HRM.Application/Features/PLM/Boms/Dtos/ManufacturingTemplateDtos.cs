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
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int SequenceNo { get; init; }
    public string? Description { get; init; }
    public Guid? ManufacturingWorkInstructionTemplateId { get; init; }
    public IReadOnlyList<ManufacturingProcessTemplateStageMachineWriteDto> Machines { get; init; } = [];
    public IReadOnlyList<ManufacturingProcessTemplateStageMachineConfigurationGroupWriteDto> MachineConfigurationGroups { get; init; } = [];
}

/// <summary>
/// Payload gọn cho một nhóm máy dùng chung lưu ý và thông số vận hành.
/// Backend vẫn lưu từng máy và từng parameter độc lập sau khi mở rộng payload này.
/// </summary>
public sealed class ManufacturingProcessTemplateStageMachineConfigurationGroupWriteDto
{
    public Guid ConfigurationGroupKey { get; init; }
    public string ConfigurationGroupName { get; init; } = string.Empty;
    public string? Note { get; init; }
    public IReadOnlyList<ManufacturingProcessTemplateStageMachineMemberWriteDto> Machines { get; init; } = [];
    public IReadOnlyList<ManufacturingProcessTemplateStageMachineParameterWriteDto> Parameters { get; init; } = [];
}

public sealed class ManufacturingProcessTemplateStageMachineMemberWriteDto
{
    public int EquipmentId { get; init; }
    public bool IsDefault { get; init; }
    public int SequenceNo { get; init; }
}

public sealed class ManufacturingProcessTemplateStageMachineWriteDto
{
    public int EquipmentId { get; init; }
    public Guid? ConfigurationGroupKey { get; init; }
    public string? ConfigurationGroupName { get; init; }
    public bool IsDefault { get; init; }
    public int SequenceNo { get; init; }
    public string? Note { get; init; }
    public IReadOnlyList<ManufacturingProcessTemplateStageMachineParameterWriteDto> Parameters { get; init; } = [];
}

public sealed class ManufacturingProcessTemplateStageMachineParameterWriteDto
{
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

public sealed class UpsertManufacturingProcessTemplateRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public DateTime? EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
    public DateTime? ExpectedUpdatedDate { get; init; }
    public IReadOnlyList<ManufacturingProcessTemplateApplicabilityWriteDto> ApplicabilityRules { get; init; } = [];
    public IReadOnlyList<ManufacturingProcessTemplateStageWriteDto> Stages { get; init; } = [];
    public IReadOnlyList<ManufacturingProcessTemplateStageTransitionWriteDto> StageTransitions { get; init; } = [];
}

public sealed class ManufacturingProcessTemplateApplicabilityWriteDto
{
    public Guid? CategoryId { get; init; }
    public HRM.Domain.Enums.Manufacturings.StepOfProduct? StepOfProduct { get; init; }
    public int Priority { get; init; }
    public string? Note { get; init; }
}

public sealed class ManufacturingProcessTemplateStageTransitionWriteDto
{
    public Guid? ManufacturingProcessTemplateStageTransitionId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string FromStageCode { get; init; } = string.Empty;
    public string ToStageCode { get; init; } = string.Empty;
    public ManufacturingStageTransitionType TransitionType { get; init; }
    public int? DefaultEventCount { get; init; }
    public int SequenceNo { get; init; }
    public string? Note { get; init; }
}

public sealed class ManufacturingProcessTemplateStageDto
{
    public Guid ManufacturingProcessTemplateStageId { get; init; }
    public string ExternalId { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
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
    public string? GroupType { get; init; }
    public int AreaId { get; init; }
    public string AreaExternalId { get; init; } = string.Empty;
    public Guid PartId { get; init; }
    public string? PartExternalId { get; init; }
    public Guid? ConfigurationGroupKey { get; init; }
    public string? ConfigurationGroupName { get; init; }
    public bool IsDefault { get; init; }
    public int SequenceNo { get; init; }
    public string? Note { get; init; }
    public IReadOnlyList<ManufacturingProcessTemplateStageMachineParameterDto> Parameters { get; init; } = [];
}

public sealed class ManufacturingProcessTemplateStageMachineParameterDto
{
    public Guid ManufacturingProcessTemplateStageMachineParameterId { get; init; }
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
    public IReadOnlyList<ManufacturingProcessTemplateApplicabilityDto> ApplicabilityRules { get; init; } = [];
    public IReadOnlyList<ManufacturingProcessTemplateStageDto> Stages { get; init; } = [];
    public IReadOnlyList<ManufacturingProcessTemplateStageTransitionDto> StageTransitions { get; init; } = [];
}

/// <summary>Read model gọn dành cho editor, không lặp cấu hình chung trên từng máy trong nhóm.</summary>
public sealed class ManufacturingProcessTemplateEditorDto
{
    public Guid ManufacturingProcessTemplateId { get; init; }
    public string ExternalId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int VersionNo { get; init; }
    public ManufacturingTemplateStatus Status { get; init; }
    public DateTime? EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
    public DateTime? UpdatedDate { get; init; }
    public IReadOnlyList<ManufacturingProcessTemplateApplicabilityDto> ApplicabilityRules { get; init; } = [];
    public IReadOnlyList<ManufacturingProcessTemplateEditorStageDto> Stages { get; init; } = [];
    public IReadOnlyList<ManufacturingProcessTemplateStageTransitionDto> StageTransitions { get; init; } = [];
}

public sealed class ManufacturingProcessTemplateEditorStageDto
{
    public Guid ManufacturingProcessTemplateStageId { get; init; }
    public string ExternalId { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int SequenceNo { get; init; }
    public string? Description { get; init; }
    public Guid? ManufacturingWorkInstructionTemplateId { get; init; }
    public string? WorkInstructionExternalId { get; init; }
    public string? WorkInstructionName { get; init; }
    public int? WorkInstructionVersionNo { get; init; }
    public IReadOnlyList<ManufacturingProcessTemplateStageMachineDto> UngroupedMachines { get; init; } = [];
    public IReadOnlyList<ManufacturingProcessTemplateStageMachineConfigurationGroupDto> MachineConfigurationGroups { get; init; } = [];
}

public sealed class ManufacturingProcessTemplateStageMachineConfigurationGroupDto
{
    public Guid ConfigurationGroupKey { get; init; }
    public string ConfigurationGroupName { get; init; } = string.Empty;
    public string? Note { get; init; }
    public bool IsConfigurationConsistent { get; init; }
    public IReadOnlyList<ManufacturingProcessTemplateStageMachineMemberDto> Machines { get; init; } = [];
    public IReadOnlyList<ManufacturingProcessTemplateSharedMachineParameterDto> Parameters { get; init; } = [];
}

public sealed class ManufacturingProcessTemplateSharedMachineParameterDto
{
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

public sealed class ManufacturingProcessTemplateStageMachineMemberDto
{
    public int EquipmentId { get; init; }
    public string EquipmentExternalId { get; init; } = string.Empty;
    public string EquipmentName { get; init; } = string.Empty;
    public string? GroupType { get; init; }
    public int AreaId { get; init; }
    public string AreaExternalId { get; init; } = string.Empty;
    public Guid PartId { get; init; }
    public string? PartExternalId { get; init; }
    public bool IsDefault { get; init; }
    public int SequenceNo { get; init; }
}

public sealed class ManufacturingProcessTemplateApplicabilityDto
{
    public Guid ManufacturingProcessTemplateApplicabilityId { get; init; }
    public Guid? CategoryId { get; init; }
    public string? CategoryExternalId { get; init; }
    public string? CategoryName { get; init; }
    public HRM.Domain.Enums.Manufacturings.StepOfProduct? StepOfProduct { get; init; }
    public int Priority { get; init; }
    public string? Note { get; init; }
}

public sealed class ManufacturingProcessTemplateSuggestionDto
{
    public Guid ManufacturingProcessTemplateId { get; init; }
    public string ExternalId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int VersionNo { get; init; }
    public int StageCount { get; init; }
    public int MatchScore { get; init; }
    public string MatchLevel { get; init; } = string.Empty;
    public int Priority { get; init; }
}

public sealed class ManufacturingProcessTemplateListItemDto
{
    public Guid ManufacturingProcessTemplateId { get; init; }
    public string ExternalId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int VersionNo { get; init; }
    public int StageCount { get; init; }
    public ManufacturingTemplateStatus Status { get; init; }
    public DateTime? EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
    public DateTime CreatedDate { get; init; }
    public DateTime? UpdatedDate { get; init; }
}

public sealed class ManufacturingProcessTemplateStageTransitionDto
{
    public Guid ManufacturingProcessTemplateStageTransitionId { get; init; }
    public string ExternalId { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public Guid FromStageId { get; init; }
    public string FromStageExternalId { get; init; } = string.Empty;
    public string FromStageCode { get; init; } = string.Empty;
    public int FromStageSequenceNo { get; init; }
    public Guid ToStageId { get; init; }
    public string ToStageExternalId { get; init; } = string.Empty;
    public string ToStageCode { get; init; } = string.Empty;
    public int ToStageSequenceNo { get; init; }
    public ManufacturingStageTransitionType TransitionType { get; init; }
    public int? DefaultEventCount { get; init; }
    public int SequenceNo { get; init; }
    public string? Note { get; init; }
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
    public IReadOnlyList<ManufacturingBomStageTransitionDto> StageTransitions { get; init; } = [];
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

public sealed class ManufacturingEquipmentOptionDto
{
    public int EquipmentId { get; init; }
    public string EquipmentExternalId { get; init; } = string.Empty;
    public string EquipmentName { get; init; } = string.Empty;
    public string? GroupType { get; init; }
    public int AreaId { get; init; }
    public string AreaExternalId { get; init; } = string.Empty;
    public Guid PartId { get; init; }
    public string? PartExternalId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
}

public sealed class ManufacturingEquipmentFilterOptionsDto
{
    public IReadOnlyList<string> GroupTypes { get; init; } = [];
    public IReadOnlyList<ManufacturingEquipmentAreaOptionDto> Areas { get; init; } = [];
}

public sealed class ManufacturingEquipmentAreaOptionDto
{
    public string ExternalId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
}

public sealed class ManufacturingProcessTemplateValidationDto
{
    public bool IsValid => Issues.Count == 0;
    public IReadOnlyList<ManufacturingProcessTemplateValidationIssueDto> Issues { get; init; } = [];
}

public sealed class ManufacturingProcessTemplateValidationIssueDto
{
    public string Code { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}
