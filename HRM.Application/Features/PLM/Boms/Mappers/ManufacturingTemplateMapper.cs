using HRM.Application.Features.PLM.Boms.Dtos;
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
        Stages = entity.Stages.OrderBy(x => x.SequenceNo).Select(x => new ManufacturingProcessTemplateStageDto
        {
            ManufacturingProcessTemplateStageId = x.ManufacturingProcessTemplateStageId,
            ExternalId = x.ExternalId,
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
                IsDefault = machine.IsDefault,
                SequenceNo = machine.SequenceNo,
                Note = machine.Note
            }).ToList()
        }).ToList()
    };
}
