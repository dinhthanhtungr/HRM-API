using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Application.Features.PLM.Boms.Rules;
using HRM.Domain.Entities.BomSchema;

namespace HRM.Application.Features.PLM.Boms.Mappers;

internal static class ManufacturingTemplateMapper
{
    internal static ManufacturingWorkInstructionTemplateDto ToDto(ManufacturingWorkInstructionTemplate entity) => new()
    {
        ManufacturingWorkInstructionTemplateId = entity.ManufacturingWorkInstructionTemplateId,
        ExternalId = entity.ExternalId,
        Name = entity.Name,
        VersionNo = entity.VersionNo,
        Status = entity.Status,
        Purpose = entity.Purpose,
        Preparation = entity.Preparation,
        Procedure = entity.Procedure,
        QualityRequirements = entity.QualityRequirements,
        SafetyNotes = entity.SafetyNotes,
        EffectiveFrom = entity.EffectiveFrom,
        EffectiveTo = entity.EffectiveTo,
        ChecklistItems = entity.ChecklistItems.OrderBy(x => x.SequenceNo).Select(x => new ManufacturingWorkInstructionChecklistDto
        {
            ManufacturingWorkInstructionChecklistItemId = x.ManufacturingWorkInstructionChecklistItemId,
            ExternalId = x.ExternalId,
            Content = x.Content,
            SequenceNo = x.SequenceNo,
            IsRequired = x.IsRequired,
            ExpectedValue = x.ExpectedValue,
            Unit = x.Unit
        }).ToList()
    };

    internal static ManufacturingProcessTemplateDto ToDto(ManufacturingProcessTemplate entity) => new()
    {
        ManufacturingProcessTemplateId = entity.ManufacturingProcessTemplateId,
        ExternalId = entity.ExternalId,
        Name = entity.Name,
        Description = entity.Description,
        VersionNo = entity.VersionNo,
        Status = entity.Status,
        EffectiveFrom = entity.EffectiveFrom,
        EffectiveTo = entity.EffectiveTo,
        ApplicabilityRules = entity.ApplicabilityRules
            .OrderByDescending(x => x.Priority)
            .ThenBy(x => x.Category != null ? x.Category.ExternalId : null)
            .ThenBy(x => x.StepOfProduct)
            .Select(x => new ManufacturingProcessTemplateApplicabilityDto
            {
                ManufacturingProcessTemplateApplicabilityId = x.ManufacturingProcessTemplateApplicabilityId,
                CategoryId = x.CategoryId,
                CategoryExternalId = x.Category != null ? x.Category.ExternalId : null,
                CategoryName = x.Category != null ? x.Category.Name : null,
                StepOfProduct = x.StepOfProduct,
                Priority = x.Priority,
                Note = x.Note
            }).ToList(),
        Stages = entity.Stages.OrderBy(x => x.SequenceNo).Select(x => new ManufacturingProcessTemplateStageDto
        {
            ManufacturingProcessTemplateStageId = x.ManufacturingProcessTemplateStageId,
            ExternalId = x.ExternalId,
            Code = x.Code,
            Name = x.Name,
            SequenceNo = x.SequenceNo,
            Description = x.Description,
            ManufacturingWorkInstructionTemplateId = x.ManufacturingWorkInstructionTemplateId,
            WorkInstructionExternalId = x.WorkInstructionTemplate?.ExternalId,
            WorkInstructionName = x.WorkInstructionTemplate?.Name,
            WorkInstructionVersionNo = x.WorkInstructionTemplate?.VersionNo,
            Machines = x.Machines.OrderBy(machine => machine.SequenceNo).Select(machine => new ManufacturingProcessTemplateStageMachineDto
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
                Parameters = machine.Parameters.OrderBy(parameter => parameter.SequenceNo).Select(parameter => new ManufacturingProcessTemplateStageMachineParameterDto
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
                }).ToList()
            }).ToList()
        }).ToList(),
        StageTransitions = entity.StageTransitions.OrderBy(x => x.SequenceNo).Select(x => new ManufacturingProcessTemplateStageTransitionDto
        {
            ManufacturingProcessTemplateStageTransitionId = x.ManufacturingProcessTemplateStageTransitionId,
            ExternalId = x.ExternalId,
            Code = x.Code,
            FromStageId = x.FromManufacturingProcessTemplateStageId,
            FromStageExternalId = x.FromStage.ExternalId,
            FromStageCode = x.FromStage.Code,
            FromStageSequenceNo = x.FromStage.SequenceNo,
            ToStageId = x.ToManufacturingProcessTemplateStageId,
            ToStageExternalId = x.ToStage.ExternalId,
            ToStageCode = x.ToStage.Code,
            ToStageSequenceNo = x.ToStage.SequenceNo,
            TransitionType = x.TransitionType,
            DefaultEventCount = x.DefaultEventCount,
            SequenceNo = x.SequenceNo,
            Note = x.Note
        }).ToList()
    };

    internal static ManufacturingProcessTemplateEditorDto ToEditorDto(ManufacturingProcessTemplate entity)
    {
        var stagesById = entity.Stages.ToDictionary(x => x.ManufacturingProcessTemplateStageId);
        return new ManufacturingProcessTemplateEditorDto
        {
            ManufacturingProcessTemplateId = entity.ManufacturingProcessTemplateId,
            ExternalId = entity.ExternalId,
            Name = entity.Name,
            Description = entity.Description,
            VersionNo = entity.VersionNo,
            Status = entity.Status,
            EffectiveFrom = entity.EffectiveFrom,
            EffectiveTo = entity.EffectiveTo,
            UpdatedDate = entity.UpdatedDate,
            ApplicabilityRules = entity.ApplicabilityRules
                .OrderByDescending(x => x.Priority)
                .ThenBy(x => x.Category != null ? x.Category.ExternalId : null)
                .ThenBy(x => x.StepOfProduct)
                .Select(x => new ManufacturingProcessTemplateApplicabilityDto
                {
                    ManufacturingProcessTemplateApplicabilityId = x.ManufacturingProcessTemplateApplicabilityId,
                    CategoryId = x.CategoryId,
                    CategoryExternalId = x.Category?.ExternalId,
                    CategoryName = x.Category?.Name,
                    StepOfProduct = x.StepOfProduct,
                    Priority = x.Priority,
                    Note = x.Note
                }).ToList(),
            StageTransitions = entity.StageTransitions.OrderBy(x => x.SequenceNo).Select(x => new ManufacturingProcessTemplateStageTransitionDto
            {
                ManufacturingProcessTemplateStageTransitionId = x.ManufacturingProcessTemplateStageTransitionId,
                ExternalId = x.ExternalId,
                Code = x.Code,
                FromStageId = x.FromManufacturingProcessTemplateStageId,
                FromStageExternalId = stagesById[x.FromManufacturingProcessTemplateStageId].ExternalId,
                FromStageCode = stagesById[x.FromManufacturingProcessTemplateStageId].Code,
                FromStageSequenceNo = stagesById[x.FromManufacturingProcessTemplateStageId].SequenceNo,
                ToStageId = x.ToManufacturingProcessTemplateStageId,
                ToStageExternalId = stagesById[x.ToManufacturingProcessTemplateStageId].ExternalId,
                ToStageCode = stagesById[x.ToManufacturingProcessTemplateStageId].Code,
                ToStageSequenceNo = stagesById[x.ToManufacturingProcessTemplateStageId].SequenceNo,
                TransitionType = x.TransitionType,
                DefaultEventCount = x.DefaultEventCount,
                SequenceNo = x.SequenceNo,
                Note = x.Note
            }).ToList(),
            Stages = entity.Stages.Where(x => x.IsActive).OrderBy(x => x.SequenceNo).Select(stage => new ManufacturingProcessTemplateEditorStageDto
            {
                ManufacturingProcessTemplateStageId = stage.ManufacturingProcessTemplateStageId,
                ExternalId = stage.ExternalId,
                Code = stage.Code,
                Name = stage.Name,
                SequenceNo = stage.SequenceNo,
                Description = stage.Description,
                ManufacturingWorkInstructionTemplateId = stage.ManufacturingWorkInstructionTemplateId,
                WorkInstructionExternalId = stage.WorkInstructionTemplate?.ExternalId,
                WorkInstructionName = stage.WorkInstructionTemplate?.Name,
                WorkInstructionVersionNo = stage.WorkInstructionTemplate?.VersionNo,
                UngroupedMachines = stage.Machines
                    .Where(machine => !machine.ConfigurationGroupKey.HasValue)
                    .OrderBy(machine => machine.SequenceNo)
                    .Select(ToMachineDto)
                    .ToList(),
                MachineConfigurationGroups = stage.Machines
                    .Where(machine => machine.ConfigurationGroupKey.HasValue)
                    .GroupBy(machine => machine.ConfigurationGroupKey!.Value)
                    .Select(group =>
                    {
                        var orderedMachines = group.OrderBy(machine => machine.SequenceNo).ToList();
                        var sharedConfiguration = orderedMachines[0];
                        return new ManufacturingProcessTemplateStageMachineConfigurationGroupDto
                        {
                            ConfigurationGroupKey = group.Key,
                            ConfigurationGroupName = sharedConfiguration.ConfigurationGroupName ?? string.Empty,
                            Note = sharedConfiguration.Note,
                            IsConfigurationConsistent = orderedMachines.Skip(1).All(machine =>
                                ManufacturingProcessTemplateMachineConfigurationRules.HasSameSharedConfiguration(sharedConfiguration, machine)),
                            Parameters = sharedConfiguration.Parameters.OrderBy(parameter => parameter.SequenceNo)
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
                                }).ToList(),
                            Machines = orderedMachines
                                .Select(machine => new ManufacturingProcessTemplateStageMachineMemberDto
                                {
                                    EquipmentId = machine.EquipmentId,
                                    EquipmentExternalId = machine.Equipment.EquipmentExternalId,
                                    EquipmentName = machine.Equipment.EquipmentName,
                                    GroupType = machine.Equipment.GroupType,
                                    AreaId = machine.Equipment.AreaId,
                                    AreaExternalId = machine.Equipment.AreaExternalId,
                                    PartId = machine.Equipment.PartId,
                                    PartExternalId = machine.Equipment.PartExternalId,
                                    IsDefault = machine.IsDefault,
                                    SequenceNo = machine.SequenceNo
                                }).ToList()
                        };
                    })
                    .OrderBy(group => group.Machines.Min(machine => machine.SequenceNo))
                    .ToList()
            }).ToList()
        };
    }

    private static ManufacturingProcessTemplateStageMachineDto ToMachineDto(ManufacturingProcessTemplateStageMachine machine) => new()
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
        Parameters = machine.Parameters.OrderBy(parameter => parameter.SequenceNo).Select(parameter => new ManufacturingProcessTemplateStageMachineParameterDto
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
        }).ToList()
    };
}
