using HRM.Domain.Entities.BomSchema;

namespace HRM.Application.Features.PLM.Boms.Services;

internal static class ManufacturingProcessTemplateSnapshotFactory
{
    internal static ManufacturingBomStage CreateStage(Guid bomVersionId, ManufacturingProcessTemplateStage source, DateTime now)
    {
        var stage = new ManufacturingBomStage
        {
            ManufacturingBomStageId = Guid.CreateVersion7(),
            BomVersionId = bomVersionId,
            ExternalId = source.ExternalId,
            Name = source.Name,
            SequenceNo = source.SequenceNo,
            Description = source.Description,
            Machines = source.Machines.OrderBy(x => x.SequenceNo).Select(x => new ManufacturingBomStageMachine
            {
                ManufacturingBomStageMachineId = Guid.CreateVersion7(),
                EquipmentId = x.EquipmentId,
                EquipmentExternalIdSnapshot = x.Equipment.EquipmentExternalId,
                EquipmentNameSnapshot = x.Equipment.EquipmentName,
                IsDefault = x.IsDefault,
                SequenceNo = x.SequenceNo,
                Note = x.Note
            }).ToList()
        };

        if (source.WorkInstructionTemplate is { } wi)
        {
            stage.WorkInstruction = new ManufacturingBomStageWorkInstruction
            {
                ManufacturingBomStageWorkInstructionId = Guid.CreateVersion7(),
                ManufacturingBomStageId = stage.ManufacturingBomStageId,
                SourceWorkInstructionTemplateId = wi.ManufacturingWorkInstructionTemplateId,
                ExternalIdSnapshot = wi.ExternalId,
                NameSnapshot = wi.Name,
                VersionNoSnapshot = wi.VersionNo,
                PurposeSnapshot = wi.Purpose,
                PreparationSnapshot = wi.Preparation,
                ProcedureSnapshot = wi.Procedure,
                QualityRequirementsSnapshot = wi.QualityRequirements,
                SafetyNotesSnapshot = wi.SafetyNotes,
                SnapshottedDate = now,
                ChecklistItems = wi.ChecklistItems.Where(x => x.IsActive).OrderBy(x => x.SequenceNo).Select(x => new ManufacturingBomStageChecklistItem
                {
                    ManufacturingBomStageChecklistItemId = Guid.CreateVersion7(),
                    SourceChecklistItemId = x.ManufacturingWorkInstructionChecklistItemId,
                    ExternalIdSnapshot = x.ExternalId,
                    ContentSnapshot = x.Content,
                    SequenceNo = x.SequenceNo,
                    IsRequired = x.IsRequired,
                    ExpectedValueSnapshot = x.ExpectedValue,
                    UnitSnapshot = x.Unit
                }).ToList()
            };
        }

        return stage;
    }
}
