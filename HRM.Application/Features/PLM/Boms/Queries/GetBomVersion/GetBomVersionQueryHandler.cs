using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Domain.Enums.Boms;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Boms.Queries.GetBomVersion;

internal sealed class GetBomVersionQueryHandler
    : IRequestHandler<GetBomVersionQuery, BomVersionDto?>
{
    private readonly IPLMReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public GetBomVersionQueryHandler(
        IPLMReadDbContext dbContext,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<BomVersionDto?> Handle(
        GetBomVersionQuery request,
        CancellationToken cancellationToken)
    {
        if (request.BomVersionId == Guid.Empty ||
            _currentUser.CompanyId is not { } companyId ||
            companyId == Guid.Empty)
        {
            return null;
        }

        return await _dbContext.BomVersions
            .AsNoTracking()
            .Where(x =>
                x.BomVersionId == request.BomVersionId &&
                x.BomDefinition.CompanyId == companyId &&
                (!request.ExpectedBomType.HasValue || x.BomDefinition.BomType == request.ExpectedBomType) &&
                x.BomDefinition.IsActive)
            .Select(x => new BomVersionDto
            {
                BomDefinitionId = x.BomDefinitionId,
                BomVersionId = x.BomVersionId,
                ProductId = x.BomDefinition.ProductId,
                Code = x.BomDefinition.ExternalId,
                Name = x.BomDefinition.Name,
                BomType = x.BomDefinition.BomType,
                VersionNo = x.VersionNo,
                Status = x.Status,
                SourceFormulaId = x.SourceFormulaId,
                SourceEngineeringBomVersionId = x.SourceEngineeringBomVersionId,
                BaseOutputQuantity = x.BaseOutputQuantity,
                OutputUnit = x.OutputUnit,
                EffectiveFrom = x.EffectiveFrom,
                EffectiveTo = x.EffectiveTo,
                ChangeReason = x.ChangeReason,
                Note = x.Note,
                ReleasedDate = x.ReleasedDate,
                Items = x.Items
                    .OrderBy(item => item.LineNo)
                    .Select(item => new BomItemDto
                    {
                        BomVersionItemId = item.BomVersionItemId,
                        LineNo = item.LineNo,
                        ItemType = item.ItemType,
                        ItemId = item.MaterialId ?? item.ComponentProductId ?? Guid.Empty,
                        CategoryId = item.CategoryId,
                        Quantity = item.Quantity,
                        Unit = item.Unit,
                        ItemCode = item.MaterialExternalIdSnapshot,
                        ItemName = item.MaterialNameSnapshot,
                        Note = item.Note
                    })
                    .ToList(),
                Stages = x.ManufacturingStages
                    .OrderBy(stage => stage.SequenceNo)
                    .Select(stage => new ManufacturingBomStageDto
                    {
                        ManufacturingBomStageId = stage.ManufacturingBomStageId,
                        Code = stage.ExternalId,
                        Name = stage.Name,
                        SequenceNo = stage.SequenceNo,
                        Description = stage.Description,
                        IsActive = stage.IsActive,
                        Machines = stage.Machines.OrderBy(machine => machine.SequenceNo).Select(machine => new ManufacturingBomStageMachineDto
                        {
                            ManufacturingBomStageMachineId = machine.ManufacturingBomStageMachineId,
                            EquipmentId = machine.EquipmentId,
                            EquipmentExternalId = machine.EquipmentExternalIdSnapshot,
                            EquipmentName = machine.EquipmentNameSnapshot,
                            IsDefault = machine.IsDefault,
                            SequenceNo = machine.SequenceNo,
                            Note = machine.Note,
                            Parameters = machine.Parameters.OrderBy(parameter => parameter.SequenceNo).Select(parameter => new ManufacturingBomStageMachineParameterDto
                            {
                                ManufacturingBomStageMachineParameterId = parameter.ManufacturingBomStageMachineParameterId,
                                ParameterCode = parameter.ParameterCodeSnapshot,
                                ParameterName = parameter.ParameterNameSnapshot,
                                TargetValue = parameter.TargetValueSnapshot,
                                MinValue = parameter.MinValueSnapshot,
                                MaxValue = parameter.MaxValueSnapshot,
                                Unit = parameter.UnitSnapshot,
                                IsRequired = parameter.IsRequiredSnapshot,
                                SequenceNo = parameter.SequenceNo,
                                Note = parameter.NoteSnapshot
                            }).ToList()
                        }).ToList(),
                        WorkInstruction = stage.WorkInstruction == null ? null : new ManufacturingBomStageWorkInstructionDto
                        {
                            ManufacturingBomStageWorkInstructionId = stage.WorkInstruction.ManufacturingBomStageWorkInstructionId,
                            SourceWorkInstructionTemplateId = stage.WorkInstruction.SourceWorkInstructionTemplateId,
                            ExternalIdSnapshot = stage.WorkInstruction.ExternalIdSnapshot,
                            NameSnapshot = stage.WorkInstruction.NameSnapshot,
                            VersionNoSnapshot = stage.WorkInstruction.VersionNoSnapshot,
                            PurposeSnapshot = stage.WorkInstruction.PurposeSnapshot,
                            PreparationSnapshot = stage.WorkInstruction.PreparationSnapshot,
                            ProcedureSnapshot = stage.WorkInstruction.ProcedureSnapshot,
                            QualityRequirementsSnapshot = stage.WorkInstruction.QualityRequirementsSnapshot,
                            SafetyNotesSnapshot = stage.WorkInstruction.SafetyNotesSnapshot,
                            ChecklistItems = stage.WorkInstruction.ChecklistItems.OrderBy(item => item.SequenceNo).Select(item => new ManufacturingBomStageChecklistItemDto
                            {
                                ManufacturingBomStageChecklistItemId = item.ManufacturingBomStageChecklistItemId,
                                ExternalIdSnapshot = item.ExternalIdSnapshot,
                                ContentSnapshot = item.ContentSnapshot,
                                SequenceNo = item.SequenceNo,
                                IsRequired = item.IsRequired,
                                ExpectedValueSnapshot = item.ExpectedValueSnapshot,
                                UnitSnapshot = item.UnitSnapshot
                            }).ToList()
                        }
                    })
                    .ToList(),
                LossRules = x.LossRules
                    .OrderBy(rule => rule.SequenceNo)
                    .Select(rule => new ManufacturingBomLossRuleDto
                    {
                        ManufacturingBomLossRuleId = rule.ManufacturingBomLossRuleId,
                        ManufacturingLossTypeId = rule.ManufacturingLossTypeId,
                        LossTypeCode = rule.LossType.ExternalId,
                        LossTypeName = rule.LossType.Name,
                        ItemLineNo = rule.BomVersionItem != null ? rule.BomVersionItem.LineNo : null,
                        StageCode = rule.ManufacturingStage != null ? rule.ManufacturingStage.ExternalId : null,
                        CalculationMethod = rule.CalculationMethod,
                        RatePercent = rule.RatePercent,
                        FixedQuantityKg = rule.FixedQuantityKg,
                        QuantityPerEventKg = rule.QuantityPerEventKg,
                        DefaultEventCount = rule.DefaultEventCount,
                        SequenceNo = rule.SequenceNo,
                        IsRecoverable = rule.IsRecoverable,
                        IncludeInMaterialRequest = rule.IncludeInMaterialRequest,
                        IsActive = rule.IsActive,
                        Note = rule.Note
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);
    }
}
