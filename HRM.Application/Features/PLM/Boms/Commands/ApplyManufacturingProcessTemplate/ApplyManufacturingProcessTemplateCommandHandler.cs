using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Application.Features.PLM.Boms.Services;
using HRM.Domain.Entities.BomSchema;
using HRM.Domain.Enums.Audits;
using HRM.Domain.Enums.Boms;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Boms.Commands.ApplyManufacturingProcessTemplate;

internal sealed class ApplyManufacturingProcessTemplateCommandHandler : IRequestHandler<ApplyManufacturingProcessTemplateCommand, OperationResult<ManufacturingProcessTemplateApplicationDto>>
{
    private readonly IPLMWriteDbContext _db; private readonly ICurrentUser _currentUser;
    public ApplyManufacturingProcessTemplateCommandHandler(IPLMWriteDbContext db, ICurrentUser currentUser) { _db = db; _currentUser = currentUser; }
    public async Task<OperationResult<ManufacturingProcessTemplateApplicationDto>> Handle(ApplyManufacturingProcessTemplateCommand command, CancellationToken cancellationToken)
    {
        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty) return OperationResult<ManufacturingProcessTemplateApplicationDto>.Fail("Current company is invalid.");
        if (!command.IsPreview && (_currentUser.EmployeeId is not { } employeeId || employeeId == Guid.Empty)) return OperationResult<ManufacturingProcessTemplateApplicationDto>.Fail("Current employee is invalid.");
        var now = DateTime.Now;
        var template = await _db.ManufacturingProcessTemplates.AsNoTracking()
            .Include(x => x.Stages).ThenInclude(x => x.WorkInstructionTemplate)!.ThenInclude(x => x!.ChecklistItems)
            .Include(x => x.Stages).ThenInclude(x => x.Machines).ThenInclude(x => x.Equipment)
            .FirstOrDefaultAsync(x => x.ManufacturingProcessTemplateId == command.ProcessTemplateId && x.CompanyId == companyId && x.IsActive && x.Status == ManufacturingTemplateStatus.Released && (!x.EffectiveFrom.HasValue || x.EffectiveFrom <= now) && (!x.EffectiveTo.HasValue || x.EffectiveTo >= now), cancellationToken);
        if (template is null) return OperationResult<ManufacturingProcessTemplateApplicationDto>.Fail("A Released and effective process template was not found.");
        var version = await _db.BomVersions.Include(x => x.BomDefinition).Include(x => x.ManufacturingStages).ThenInclude(x => x.Items).Include(x => x.ManufacturingStages).ThenInclude(x => x.LossRules).Include(x => x.ManufacturingStages).ThenInclude(x => x.Machines).Include(x => x.ManufacturingStageTransitions)
            .FirstOrDefaultAsync(x => x.BomVersionId == command.BomVersionId && x.BomDefinition.CompanyId == companyId && x.BomDefinition.BomType == BomType.Manufacturing, cancellationToken);
        if (version is null) return OperationResult<ManufacturingProcessTemplateApplicationDto>.Fail("Manufacturing BOM version was not found.");
        if (version.Status != BomVersionStatus.Draft) return OperationResult<ManufacturingProcessTemplateApplicationDto>.Fail("Only Draft Manufacturing BOM versions can receive a process template.");
        var hasDependencies = version.ManufacturingStageTransitions.Count > 0 || version.ManufacturingStages.Any(x => x.Items.Count > 0 || x.LossRules.Count > 0);
        if (!command.IsPreview && hasDependencies)
            return OperationResult<ManufacturingProcessTemplateApplicationDto>.Fail("Existing stage assignments, transitions or loss rules must be cleared before replacing the process template.");

        var stages = template.Stages.Where(x => x.IsActive).OrderBy(x => x.SequenceNo).Select(x => ManufacturingProcessTemplateSnapshotFactory.CreateStage(version.BomVersionId, x, now)).ToList();
        if (!command.IsPreview)
        {
            _db.ManufacturingBomStageMachines.RemoveRange(version.ManufacturingStages.SelectMany(x => x.Machines));
            _db.ManufacturingBomStages.RemoveRange(version.ManufacturingStages);
            await _db.ManufacturingBomStages.AddRangeAsync(stages, cancellationToken);
            _db.AuditLogs.Add(BomAudit.Create(companyId, _currentUser.EmployeeId!.Value, "bom_versions", version.BomVersionId,
                "ApplyManufacturingProcessTemplate", new { template.ManufacturingProcessTemplateId, template.ExternalId, template.VersionNo, StageCount = stages.Count }, actionType: AuditActionType.Update));
            await _db.SaveChangesAsync(cancellationToken);
        }
        return OperationResult<ManufacturingProcessTemplateApplicationDto>.Ok(new ManufacturingProcessTemplateApplicationDto
        {
            BomVersionId = version.BomVersionId, ProcessTemplateId = template.ManufacturingProcessTemplateId, ProcessTemplateExternalId = template.ExternalId,
            ProcessTemplateVersionNo = template.VersionNo, IsPreview = command.IsPreview,
            CanApply = !hasDependencies,
            BlockingReasons = hasDependencies ? [new ManufacturingProcessApplicationIssueDto { Code = "STAGES_HAVE_DEPENDENCIES", Message = "Existing stages have item assignments, transitions or loss rules." }] : [],
            Stages = stages.Select(x => new ManufacturingProcessApplicationStageDto
            {
                ExternalId = x.ExternalId, Name = x.Name, SequenceNo = x.SequenceNo, WorkInstruction = Map(x.WorkInstruction, command.IsPreview),
                Machines = x.Machines.OrderBy(machine => machine.SequenceNo).Select(machine => new ManufacturingBomStageMachineDto
                {
                    ManufacturingBomStageMachineId = machine.ManufacturingBomStageMachineId, EquipmentId = machine.EquipmentId,
                    EquipmentExternalId = machine.EquipmentExternalIdSnapshot, EquipmentName = machine.EquipmentNameSnapshot,
                    IsDefault = machine.IsDefault, SequenceNo = machine.SequenceNo, Note = machine.Note
                }).ToList()
            }).ToList()
        });
    }

    private static ManufacturingBomStageWorkInstructionDto? Map(ManufacturingBomStageWorkInstruction? wi, bool preview) => wi is null ? null : new()
    {
        ManufacturingBomStageWorkInstructionId = preview ? null : wi.ManufacturingBomStageWorkInstructionId, SourceWorkInstructionTemplateId = wi.SourceWorkInstructionTemplateId,
        ExternalIdSnapshot = wi.ExternalIdSnapshot, NameSnapshot = wi.NameSnapshot, VersionNoSnapshot = wi.VersionNoSnapshot, PurposeSnapshot = wi.PurposeSnapshot,
        PreparationSnapshot = wi.PreparationSnapshot, ProcedureSnapshot = wi.ProcedureSnapshot, QualityRequirementsSnapshot = wi.QualityRequirementsSnapshot, SafetyNotesSnapshot = wi.SafetyNotesSnapshot,
        ChecklistItems = wi.ChecklistItems.OrderBy(x => x.SequenceNo).Select(x => new ManufacturingBomStageChecklistItemDto
        {
            ManufacturingBomStageChecklistItemId = preview ? null : x.ManufacturingBomStageChecklistItemId, ExternalIdSnapshot = x.ExternalIdSnapshot, ContentSnapshot = x.ContentSnapshot,
            SequenceNo = x.SequenceNo, IsRequired = x.IsRequired, ExpectedValueSnapshot = x.ExpectedValueSnapshot, UnitSnapshot = x.UnitSnapshot
        }).ToList()
    };
}
