using HRM.Application.Abstractions.Commons.ExternalIds;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Application.Features.PLM.Boms.Mappers;
using HRM.Application.Features.PLM.Boms.Services;
using HRM.Domain.Entities.BomSchema;
using HRM.Domain.Enums.Audits;
using HRM.Domain.Enums.Boms;
using HRM.Domain.Enums.Category;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Boms.Commands.UpsertManufacturingProcessTemplate;

internal sealed class UpsertManufacturingProcessTemplateCommandHandler : IRequestHandler<UpsertManufacturingProcessTemplateCommand, OperationResult<ManufacturingProcessTemplateDto>>
{
    private readonly IPLMWriteDbContext _db; private readonly ICurrentUser _currentUser; private readonly IExternalIdService _externalIdService;
    public UpsertManufacturingProcessTemplateCommandHandler(IPLMWriteDbContext db, ICurrentUser currentUser, IExternalIdService externalIdService) { _db = db; _currentUser = currentUser; _externalIdService = externalIdService; }

    public async Task<OperationResult<ManufacturingProcessTemplateDto>> Handle(UpsertManufacturingProcessTemplateCommand command, CancellationToken cancellationToken)
    {
        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty || _currentUser.EmployeeId is not { } employeeId || employeeId == Guid.Empty) return OperationResult<ManufacturingProcessTemplateDto>.Fail("Current company or employee is invalid.");
        var request = command.Request;
        if (string.IsNullOrWhiteSpace(request.Name) || request.Stages.Count == 0) return OperationResult<ManufacturingProcessTemplateDto>.Fail("Name and at least one stage are required.");
        if (request.EffectiveTo < request.EffectiveFrom) return OperationResult<ManufacturingProcessTemplateDto>.Fail("EffectiveTo must not be earlier than EffectiveFrom.");
        if (request.Stages.Any(x => string.IsNullOrWhiteSpace(x.Name) || x.SequenceNo <= 0) || request.Stages.GroupBy(x => x.SequenceNo).Any(x => x.Count() > 1)) return OperationResult<ManufacturingProcessTemplateDto>.Fail("Stage Name and SequenceNo must be valid and SequenceNo must be unique.");
        var submittedStageIds = request.Stages.Where(x => x.ManufacturingProcessTemplateStageId.HasValue).Select(x => x.ManufacturingProcessTemplateStageId!.Value).ToList();
        if (submittedStageIds.Count != submittedStageIds.Distinct().Count()) return OperationResult<ManufacturingProcessTemplateDto>.Fail("Stage IDs must be unique.");
        if (request.Stages.Any(x => x.Machines.Any(m => m.EquipmentId <= 0 || m.SequenceNo <= 0) || x.Machines.GroupBy(m => m.EquipmentId).Any(g => g.Count() > 1) || x.Machines.GroupBy(m => m.SequenceNo).Any(g => g.Count() > 1) || x.Machines.Count(m => m.IsDefault) > 1)) return OperationResult<ManufacturingProcessTemplateDto>.Fail("Each stage machine list must have unique EquipmentId and SequenceNo, and at most one default machine.");

        var instructionIds = request.Stages.Where(x => x.ManufacturingWorkInstructionTemplateId.HasValue).Select(x => x.ManufacturingWorkInstructionTemplateId!.Value).Distinct().ToList();
        var validInstructions = await _db.ManufacturingWorkInstructionTemplates.AsNoTracking().Where(x => instructionIds.Contains(x.ManufacturingWorkInstructionTemplateId) && x.CompanyId == companyId && x.Status == ManufacturingTemplateStatus.Released && x.IsActive).Select(x => x.ManufacturingWorkInstructionTemplateId).ToListAsync(cancellationToken);
        if (validInstructions.Count != instructionIds.Count) return OperationResult<ManufacturingProcessTemplateDto>.Fail("Every Work Instruction must be Released, active and belong to the current company.");
        var equipmentIds = request.Stages.SelectMany(x => x.Machines).Select(x => x.EquipmentId).Distinct().ToList();
        var equipments = await _db.EquipmentsMro.AsNoTracking().Where(x => equipmentIds.Contains(x.EquipmentId) && x.FactoryId == companyId).ToDictionaryAsync(x => x.EquipmentId, cancellationToken);
        if (equipments.Count != equipmentIds.Count) return OperationResult<ManufacturingProcessTemplateDto>.Fail("Every machine must belong to the current company.");

        ManufacturingProcessTemplate entity;
        var isCreate = !command.TemplateId.HasValue;
        if (command.TemplateId is { } id)
        {
            entity = await _db.ManufacturingProcessTemplates.Include(x => x.Stages).ThenInclude(x => x.Machines).FirstOrDefaultAsync(x => x.ManufacturingProcessTemplateId == id && x.CompanyId == companyId, cancellationToken) ?? null!;
            if (entity is null) return OperationResult<ManufacturingProcessTemplateDto>.Fail("Process template was not found.");
            if (entity.Status != ManufacturingTemplateStatus.Draft) return OperationResult<ManufacturingProcessTemplateDto>.Fail("Only Draft process templates can be changed.");
            if (submittedStageIds.Except(entity.Stages.Select(x => x.ManufacturingProcessTemplateStageId)).Any()) return OperationResult<ManufacturingProcessTemplateDto>.Fail("A stage does not belong to this process template.");
            var removedStages = entity.Stages.Where(x => !submittedStageIds.Contains(x.ManufacturingProcessTemplateStageId)).ToList();
            _db.ManufacturingProcessTemplateStageMachines.RemoveRange(removedStages.SelectMany(x => x.Machines));
            _db.ManufacturingProcessTemplateStages.RemoveRange(removedStages);
            entity.UpdatedDate = DateTime.Now; entity.UpdatedBy = employeeId;
        }
        else
        {
            if (submittedStageIds.Count > 0) return OperationResult<ManufacturingProcessTemplateDto>.Fail("New process templates cannot reference existing stage IDs.");
            entity = new ManufacturingProcessTemplate
            {
                ManufacturingProcessTemplateId = Guid.CreateVersion7(), CompanyId = companyId,
                ExternalId = await _externalIdService.GenerateGlobalCodeAsync(companyId, DocumentPrefix.MPT.ToString(), cancellationToken),
                VersionNo = 1, CreatedDate = DateTime.Now, CreatedBy = employeeId
            };
            await _db.ManufacturingProcessTemplates.AddAsync(entity, cancellationToken);
        }

        entity.Name = request.Name.Trim(); entity.Description = Normalize(request.Description); entity.EffectiveFrom = request.EffectiveFrom; entity.EffectiveTo = request.EffectiveTo;
        var existingById = entity.Stages.ToDictionary(x => x.ManufacturingProcessTemplateStageId);
        var updatedStages = new List<ManufacturingProcessTemplateStage>();
        foreach (var source in request.Stages)
        {
            ManufacturingProcessTemplateStage stage;
            if (source.ManufacturingProcessTemplateStageId is { } stageId)
            {
                stage = existingById[stageId];
                _db.ManufacturingProcessTemplateStageMachines.RemoveRange(stage.Machines);
            }
            else
            {
                stage = new ManufacturingProcessTemplateStage
                {
                    ManufacturingProcessTemplateStageId = Guid.CreateVersion7(), ManufacturingProcessTemplateId = entity.ManufacturingProcessTemplateId,
                    ExternalId = await _externalIdService.GenerateGlobalCodeAsync(companyId, DocumentPrefix.MPS.ToString(), cancellationToken)
                };
            }
            stage.Name = source.Name.Trim(); stage.SequenceNo = source.SequenceNo; stage.Description = Normalize(source.Description); stage.ManufacturingWorkInstructionTemplateId = source.ManufacturingWorkInstructionTemplateId;
            stage.Machines = source.Machines.Select(machine => new ManufacturingProcessTemplateStageMachine
            {
                ManufacturingProcessTemplateStageMachineId = Guid.CreateVersion7(), EquipmentId = machine.EquipmentId, Equipment = equipments[machine.EquipmentId],
                IsDefault = machine.IsDefault, SequenceNo = machine.SequenceNo, Note = Normalize(machine.Note)
            }).ToList();
            updatedStages.Add(stage);
        }
        entity.Stages = updatedStages;
        _db.AuditLogs.Add(BomAudit.Create(companyId, employeeId, "manufacturing_process_templates", entity.ManufacturingProcessTemplateId,
            isCreate ? "CreateManufacturingProcessTemplate" : "UpdateManufacturingProcessTemplate", new { entity.ExternalId, entity.VersionNo, StageCount = entity.Stages.Count }, actionType: isCreate ? AuditActionType.Create : AuditActionType.Update));
        await _db.SaveChangesAsync(cancellationToken);
        var responseEntity = await _db.ManufacturingProcessTemplates.AsNoTracking().Include(x => x.Stages).ThenInclude(x => x.WorkInstructionTemplate).Include(x => x.Stages).ThenInclude(x => x.Machines).ThenInclude(x => x.Equipment).FirstAsync(x => x.ManufacturingProcessTemplateId == entity.ManufacturingProcessTemplateId, cancellationToken);
        return OperationResult<ManufacturingProcessTemplateDto>.Ok(ManufacturingTemplateMapper.ToDto(responseEntity));
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
