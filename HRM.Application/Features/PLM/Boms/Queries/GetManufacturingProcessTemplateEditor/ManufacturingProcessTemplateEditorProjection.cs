using System.Linq.Expressions;
using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Domain.Entities.BomSchema;
using HRM.Domain.Enums.Boms;

namespace HRM.Application.Features.PLM.Boms.Queries.GetManufacturingProcessTemplateEditor;

internal static class ManufacturingProcessTemplateEditorProjection
{
    internal static readonly Expression<Func<ManufacturingProcessTemplate, ManufacturingProcessTemplateEditorReadModel>> Selector =
        template => new ManufacturingProcessTemplateEditorReadModel
        {
            ManufacturingProcessTemplateId = template.ManufacturingProcessTemplateId,
            ExternalId = template.ExternalId,
            Name = template.Name,
            Description = template.Description,
            VersionNo = template.VersionNo,
            Status = template.Status,
            EffectiveFrom = template.EffectiveFrom,
            EffectiveTo = template.EffectiveTo,
            UpdatedDate = template.UpdatedDate,
            ApplicabilityRules = template.ApplicabilityRules
                .OrderByDescending(rule => rule.Priority)
                .ThenBy(rule => rule.Category != null ? rule.Category.ExternalId : null)
                .ThenBy(rule => rule.StepOfProduct)
                .Select(rule => new ManufacturingProcessTemplateApplicabilityDto
                {
                    ManufacturingProcessTemplateApplicabilityId = rule.ManufacturingProcessTemplateApplicabilityId,
                    CategoryId = rule.CategoryId,
                    CategoryExternalId = rule.Category != null ? rule.Category.ExternalId : null,
                    CategoryName = rule.Category != null ? rule.Category.Name : null,
                    StepOfProduct = rule.StepOfProduct,
                    Priority = rule.Priority,
                    Note = rule.Note
                })
                .ToList(),
            Stages = template.Stages
                .Where(stage => stage.IsActive)
                .OrderBy(stage => stage.SequenceNo)
                .Select(stage => new ManufacturingProcessTemplateEditorStageReadModel
                {
                    ManufacturingProcessTemplateStageId = stage.ManufacturingProcessTemplateStageId,
                    ExternalId = stage.ExternalId,
                    Code = stage.Code,
                    Name = stage.Name,
                    SequenceNo = stage.SequenceNo,
                    Description = stage.Description,
                    ManufacturingWorkInstructionTemplateId = stage.ManufacturingWorkInstructionTemplateId,
                    WorkInstructionExternalId = stage.WorkInstructionTemplate != null ? stage.WorkInstructionTemplate.ExternalId : null,
                    WorkInstructionName = stage.WorkInstructionTemplate != null ? stage.WorkInstructionTemplate.Name : null,
                    WorkInstructionVersionNo = stage.WorkInstructionTemplate != null ? stage.WorkInstructionTemplate.VersionNo : null,
                    Machines = stage.Machines
                        .OrderBy(machine => machine.SequenceNo)
                        .Select(machine => new ManufacturingProcessTemplateStageMachineDto
                        {
                            EquipmentId = machine.EquipmentId,
                            EquipmentExternalId = machine.Equipment.EquipmentExternalId,
                            EquipmentName = machine.Equipment.EquipmentName,
                            GroupType = machine.Equipment.GroupType,
                            AreaId = machine.Equipment.AreaId,
                            AreaExternalId = machine.Equipment.AreaExternalId,
                            PartId = machine.Equipment.PartId,
                            PartExternalId = machine.Equipment.PartExternalId,
                            ConfigurationGroupKey = machine.ConfigurationGroupKey,
                            ConfigurationGroupName = machine.ConfigurationGroupName,
                            IsDefault = machine.IsDefault,
                            SequenceNo = machine.SequenceNo,
                            Note = machine.Note,
                            Parameters = machine.Parameters
                                .OrderBy(parameter => parameter.SequenceNo)
                                .Select(parameter => new ManufacturingProcessTemplateStageMachineParameterDto
                                {
                                    ManufacturingProcessTemplateStageMachineParameterId = parameter.ManufacturingProcessTemplateStageMachineParameterId,
                                    ParameterCode = parameter.ParameterCode,
                                    ParameterName = parameter.ParameterName,
                                    TargetValue = parameter.TargetValue,
                                    MinValue = parameter.MinValue,
                                    MaxValue = parameter.MaxValue,
                                    Unit = parameter.Unit,
                                    IsRequired = parameter.IsRequired,
                                    SequenceNo = parameter.SequenceNo,
                                    Note = parameter.Note
                                })
                                .ToList()
                        })
                        .ToList()
                })
                .ToList(),
            StageTransitions = template.StageTransitions
                .OrderBy(transition => transition.SequenceNo)
                .Select(transition => new ManufacturingProcessTemplateStageTransitionDto
                {
                    ManufacturingProcessTemplateStageTransitionId = transition.ManufacturingProcessTemplateStageTransitionId,
                    ExternalId = transition.ExternalId,
                    Code = transition.Code,
                    FromStageId = transition.FromManufacturingProcessTemplateStageId,
                    FromStageExternalId = transition.FromStage.ExternalId,
                    FromStageCode = transition.FromStage.Code,
                    FromStageSequenceNo = transition.FromStage.SequenceNo,
                    ToStageId = transition.ToManufacturingProcessTemplateStageId,
                    ToStageExternalId = transition.ToStage.ExternalId,
                    ToStageCode = transition.ToStage.Code,
                    ToStageSequenceNo = transition.ToStage.SequenceNo,
                    TransitionType = transition.TransitionType,
                    DefaultEventCount = transition.DefaultEventCount,
                    SequenceNo = transition.SequenceNo,
                    Note = transition.Note
                })
                .ToList()
        };

    internal static ManufacturingProcessTemplateEditorDto ToDto(ManufacturingProcessTemplateEditorReadModel source) => new()
    {
        ManufacturingProcessTemplateId = source.ManufacturingProcessTemplateId,
        ExternalId = source.ExternalId,
        Name = source.Name,
        Description = source.Description,
        VersionNo = source.VersionNo,
        Status = source.Status,
        EffectiveFrom = source.EffectiveFrom,
        EffectiveTo = source.EffectiveTo,
        UpdatedDate = source.UpdatedDate,
        ApplicabilityRules = source.ApplicabilityRules,
        StageTransitions = source.StageTransitions,
        Stages = source.Stages.Select(ToStageDto).ToList()
    };

    private static ManufacturingProcessTemplateEditorStageDto ToStageDto(ManufacturingProcessTemplateEditorStageReadModel stage) => new()
    {
        ManufacturingProcessTemplateStageId = stage.ManufacturingProcessTemplateStageId,
        ExternalId = stage.ExternalId,
        Code = stage.Code,
        Name = stage.Name,
        SequenceNo = stage.SequenceNo,
        Description = stage.Description,
        ManufacturingWorkInstructionTemplateId = stage.ManufacturingWorkInstructionTemplateId,
        WorkInstructionExternalId = stage.WorkInstructionExternalId,
        WorkInstructionName = stage.WorkInstructionName,
        WorkInstructionVersionNo = stage.WorkInstructionVersionNo,
        UngroupedMachines = stage.Machines
            .Where(machine => !machine.ConfigurationGroupKey.HasValue)
            .OrderBy(machine => machine.SequenceNo)
            .ToList(),
        MachineConfigurationGroups = stage.Machines
            .Where(machine => machine.ConfigurationGroupKey.HasValue)
            .GroupBy(machine => machine.ConfigurationGroupKey!.Value)
            .Select(ToConfigurationGroupDto)
            .OrderBy(group => group.Machines.Min(machine => machine.SequenceNo))
            .ToList()
    };

    private static ManufacturingProcessTemplateStageMachineConfigurationGroupDto ToConfigurationGroupDto(
        IGrouping<Guid, ManufacturingProcessTemplateStageMachineDto> group)
    {
        var machines = group.OrderBy(machine => machine.SequenceNo).ToList();
        var sharedConfiguration = machines[0];
        return new ManufacturingProcessTemplateStageMachineConfigurationGroupDto
        {
            ConfigurationGroupKey = group.Key,
            ConfigurationGroupName = sharedConfiguration.ConfigurationGroupName ?? string.Empty,
            Note = sharedConfiguration.Note,
            IsConfigurationConsistent = machines.Skip(1).All(machine =>
                HasSameSharedConfiguration(sharedConfiguration, machine)),
            Parameters = sharedConfiguration.Parameters
                .OrderBy(parameter => parameter.SequenceNo)
                .Select(parameter => new ManufacturingProcessTemplateSharedMachineParameterDto
                {
                    ParameterCode = parameter.ParameterCode,
                    ParameterName = parameter.ParameterName,
                    TargetValue = parameter.TargetValue,
                    MinValue = parameter.MinValue,
                    MaxValue = parameter.MaxValue,
                    Unit = parameter.Unit,
                    IsRequired = parameter.IsRequired,
                    SequenceNo = parameter.SequenceNo,
                    Note = parameter.Note
                })
                .ToList(),
            Machines = machines.Select(machine => new ManufacturingProcessTemplateStageMachineMemberDto
            {
                EquipmentId = machine.EquipmentId,
                EquipmentExternalId = machine.EquipmentExternalId,
                EquipmentName = machine.EquipmentName,
                GroupType = machine.GroupType,
                AreaId = machine.AreaId,
                AreaExternalId = machine.AreaExternalId,
                PartId = machine.PartId,
                PartExternalId = machine.PartExternalId,
                IsDefault = machine.IsDefault,
                SequenceNo = machine.SequenceNo
            }).ToList()
        };
    }

    private static bool HasSameSharedConfiguration(
        ManufacturingProcessTemplateStageMachineDto left,
        ManufacturingProcessTemplateStageMachineDto right)
    {
        if (!SameText(left.ConfigurationGroupName, right.ConfigurationGroupName, StringComparison.OrdinalIgnoreCase) ||
            !SameText(left.Note, right.Note, StringComparison.Ordinal))
            return false;

        var leftParameters = left.Parameters
            .OrderBy(parameter => parameter.SequenceNo)
            .ThenBy(parameter => parameter.ParameterCode, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var rightParameters = right.Parameters
            .OrderBy(parameter => parameter.SequenceNo)
            .ThenBy(parameter => parameter.ParameterCode, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (leftParameters.Count != rightParameters.Count)
            return false;

        for (var index = 0; index < leftParameters.Count; index++)
        {
            var a = leftParameters[index];
            var b = rightParameters[index];
            if (!string.Equals(a.ParameterCode.Trim(), b.ParameterCode.Trim(), StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(a.ParameterName.Trim(), b.ParameterName.Trim(), StringComparison.Ordinal) ||
                a.TargetValue != b.TargetValue ||
                a.MinValue != b.MinValue ||
                a.MaxValue != b.MaxValue ||
                !string.Equals(a.Unit.Trim(), b.Unit.Trim(), StringComparison.OrdinalIgnoreCase) ||
                a.IsRequired != b.IsRequired ||
                a.SequenceNo != b.SequenceNo ||
                !SameText(a.Note, b.Note, StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    private static bool SameText(string? left, string? right, StringComparison comparison)
        => string.Equals(Normalize(left), Normalize(right), comparison);

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

internal sealed class ManufacturingProcessTemplateEditorReadModel
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
    public List<ManufacturingProcessTemplateApplicabilityDto> ApplicabilityRules { get; init; } = [];
    public List<ManufacturingProcessTemplateEditorStageReadModel> Stages { get; init; } = [];
    public List<ManufacturingProcessTemplateStageTransitionDto> StageTransitions { get; init; } = [];
}

internal sealed class ManufacturingProcessTemplateEditorStageReadModel
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
    public List<ManufacturingProcessTemplateStageMachineDto> Machines { get; init; } = [];
}
