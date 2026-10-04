using HRM.Application.Features.PLM.Boms.Dtos;

namespace HRM.Application.Features.PLM.Boms.Commands.UpsertManufacturingProcessTemplate;

/// <summary>
/// Mở rộng payload nhóm gọn thành danh sách máy phẳng để giữ nguyên mô hình lưu trữ hiện tại.
/// </summary>
internal static class ManufacturingProcessTemplateMachineGroupPayloadNormalizer
{
    public static bool TryExpand(
        UpsertManufacturingProcessTemplateRequest source,
        out UpsertManufacturingProcessTemplateRequest expanded,
        out string? error)
    {
        if (source.Stages is null || source.ApplicabilityRules is null || source.StageTransitions is null)
        {
            expanded = source;
            error = "Stages, ApplicabilityRules and StageTransitions must be arrays.";
            return false;
        }

        foreach (var stage in source.Stages)
        {
            if (stage.Machines is null || stage.MachineConfigurationGroups is null)
            {
                expanded = source;
                error = $"Machines and MachineConfigurationGroups of stage {stage.Code} must be arrays.";
                return false;
            }

            if (stage.Machines.Any(machine => machine.Parameters is null) ||
                stage.MachineConfigurationGroups.Any(group => group.Machines is null || group.Parameters is null))
            {
                expanded = source;
                error = $"Machine members and parameters of stage {stage.Code} must be arrays.";
                return false;
            }

            if (stage.Machines.Any(machine =>
                    machine.ConfigurationGroupKey == Guid.Empty ||
                    machine.ConfigurationGroupKey.HasValue && string.IsNullOrWhiteSpace(machine.ConfigurationGroupName) ||
                    !machine.ConfigurationGroupKey.HasValue && !string.IsNullOrWhiteSpace(machine.ConfigurationGroupName)))
            {
                expanded = source;
                error = $"Stage {stage.Code} has an invalid machine configuration group key or name.";
                return false;
            }

            if (stage.MachineConfigurationGroups.Any(group =>
                    group.ConfigurationGroupKey == Guid.Empty ||
                    string.IsNullOrWhiteSpace(group.ConfigurationGroupName) ||
                    group.ConfigurationGroupName.Trim().Length > 200 ||
                    group.Machines.Count == 0))
            {
                expanded = source;
                error = $"Stage {stage.Code} has an invalid machine configuration group. Key, name and at least one machine are required.";
                return false;
            }

            if (stage.MachineConfigurationGroups
                .GroupBy(group => group.ConfigurationGroupKey)
                .Any(group => group.Count() > 1))
            {
                expanded = source;
                error = $"ConfigurationGroupKey must be unique within stage {stage.Code}.";
                return false;
            }

            var compactGroupKeys = stage.MachineConfigurationGroups
                .Select(group => group.ConfigurationGroupKey)
                .ToHashSet();
            if (stage.Machines.Any(machine =>
                    machine.ConfigurationGroupKey.HasValue &&
                    compactGroupKeys.Contains(machine.ConfigurationGroupKey.Value)))
            {
                expanded = source;
                error = $"Stage {stage.Code} cannot submit the same ConfigurationGroupKey in both Machines and MachineConfigurationGroups.";
                return false;
            }

            if (stage.MachineConfigurationGroups.SelectMany(group => group.Machines)
                .Any(machine => machine.EquipmentId <= 0 || machine.SequenceNo <= 0))
            {
                expanded = source;
                error = $"Every grouped machine in stage {stage.Code} must have a valid EquipmentId and SequenceNo.";
                return false;
            }
        }

        expanded = new UpsertManufacturingProcessTemplateRequest
        {
            Name = source.Name,
            Description = source.Description,
            EffectiveFrom = source.EffectiveFrom,
            EffectiveTo = source.EffectiveTo,
            ExpectedUpdatedDate = source.ExpectedUpdatedDate,
            ApplicabilityRules = source.ApplicabilityRules,
            StageTransitions = source.StageTransitions,
            Stages = source.Stages.Select(stage => new ManufacturingProcessTemplateStageWriteDto
            {
                ManufacturingProcessTemplateStageId = stage.ManufacturingProcessTemplateStageId,
                Code = stage.Code,
                Name = stage.Name,
                SequenceNo = stage.SequenceNo,
                Description = stage.Description,
                ManufacturingWorkInstructionTemplateId = stage.ManufacturingWorkInstructionTemplateId,
                Machines = stage.Machines.Concat(stage.MachineConfigurationGroups.SelectMany(group =>
                    group.Machines.Select(machine => new ManufacturingProcessTemplateStageMachineWriteDto
                    {
                        EquipmentId = machine.EquipmentId,
                        ConfigurationGroupKey = group.ConfigurationGroupKey,
                        ConfigurationGroupName = group.ConfigurationGroupName,
                        IsDefault = machine.IsDefault,
                        SequenceNo = machine.SequenceNo,
                        Note = group.Note,
                        Parameters = group.Parameters
                    }))).ToList()
            }).ToList()
        };
        error = null;
        return true;
    }
}
