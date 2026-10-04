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

internal sealed class ApplyManufacturingProcessTemplateCommandHandler
    : IRequestHandler<
        ApplyManufacturingProcessTemplateCommand,
        OperationResult<ManufacturingProcessTemplateApplicationDto>>
{
    private readonly IPLMWriteDbContext _db;
    private readonly ICurrentUser _currentUser;

    public ApplyManufacturingProcessTemplateCommandHandler(
        IPLMWriteDbContext db,
        ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<OperationResult<ManufacturingProcessTemplateApplicationDto>> Handle(
        ApplyManufacturingProcessTemplateCommand command,
        CancellationToken cancellationToken)
    {
        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty)
        {
            return OperationResult<ManufacturingProcessTemplateApplicationDto>.Fail(
                "Current company is invalid.");
        }

        if (!command.IsPreview &&
            (_currentUser.EmployeeId is not { } employeeId || employeeId == Guid.Empty))
        {
            return OperationResult<ManufacturingProcessTemplateApplicationDto>.Fail(
                "Current employee is invalid.");
        }

        // Load one released, active and currently effective process template.
        var now = DateTime.Now;
        var template = await _db.ManufacturingProcessTemplates
            .AsNoTracking()
            .Include(processTemplate => processTemplate.Stages)
                .ThenInclude(stage => stage.WorkInstructionTemplate)!
                .ThenInclude(instruction => instruction!.ChecklistItems)
            .Include(processTemplate => processTemplate.Stages)
                .ThenInclude(stage => stage.Machines)
                .ThenInclude(machine => machine.Equipment)
            .Include(processTemplate => processTemplate.Stages)
                .ThenInclude(stage => stage.Machines)
                .ThenInclude(machine => machine.Parameters)
            .Include(processTemplate => processTemplate.StageTransitions)
            .FirstOrDefaultAsync(
                processTemplate =>
                    processTemplate.ManufacturingProcessTemplateId == command.ProcessTemplateId &&
                    processTemplate.CompanyId == companyId &&
                    processTemplate.IsActive &&
                    processTemplate.Status == ManufacturingTemplateStatus.Released &&
                    (!processTemplate.EffectiveFrom.HasValue || processTemplate.EffectiveFrom <= now) &&
                    (!processTemplate.EffectiveTo.HasValue || processTemplate.EffectiveTo >= now),
                cancellationToken);
        if (template is null)
        {
            return OperationResult<ManufacturingProcessTemplateApplicationDto>.Fail(
                "A Released and effective process template was not found.");
        }

        // The target must be a Draft Manufacturing BOM inside the same company.
        var version = await _db.BomVersions
            .Include(bomVersion => bomVersion.BomDefinition)
            .Include(bomVersion => bomVersion.ManufacturingStages)
                .ThenInclude(stage => stage.Items)
            .Include(bomVersion => bomVersion.ManufacturingStages)
                .ThenInclude(stage => stage.LossRules)
            .Include(bomVersion => bomVersion.ManufacturingStages)
                .ThenInclude(stage => stage.Machines)
            .Include(bomVersion => bomVersion.ManufacturingStageTransitions)
            .FirstOrDefaultAsync(
                bomVersion => bomVersion.BomVersionId == command.BomVersionId &&
                              bomVersion.BomDefinition.CompanyId == companyId &&
                              bomVersion.BomDefinition.BomType == BomType.Manufacturing,
                cancellationToken);
        if (version is null)
        {
            return OperationResult<ManufacturingProcessTemplateApplicationDto>.Fail(
                "Manufacturing BOM version was not found.");
        }

        if (version.Status != BomVersionStatus.Draft)
        {
            return OperationResult<ManufacturingProcessTemplateApplicationDto>.Fail(
                "Only Draft Manufacturing BOM versions can receive a process template.");
        }

        var hasDependencies =
            version.ManufacturingStageTransitions.Count > 0 ||
            version.ManufacturingStages.Any(stage =>
                stage.Items.Count > 0 || stage.LossRules.Count > 0);
        if (!command.IsPreview && hasDependencies)
        {
            return OperationResult<ManufacturingProcessTemplateApplicationDto>.Fail(
                "Existing stage assignments, transitions or loss rules must be cleared before replacing the process template.");
        }

        // Build detached stage and transition snapshots for preview or persistence.
        var stages = template.Stages
            .Where(stage => stage.IsActive)
            .OrderBy(stage => stage.SequenceNo)
            .Select(stage => ManufacturingProcessTemplateSnapshotFactory.CreateStage(
                version.BomVersionId,
                stage,
                now))
            .ToList();
        var targetStageBySourceId = template.Stages
            .Where(stage => stage.IsActive)
            .ToDictionary(
                stage => stage.ManufacturingProcessTemplateStageId,
                sourceStage => stages.Single(stage =>
                    string.Equals(
                        stage.ExternalId,
                        sourceStage.ExternalId,
                        StringComparison.OrdinalIgnoreCase)));
        var transitions = template.StageTransitions
            .Where(transition =>
                targetStageBySourceId.ContainsKey(
                    transition.FromManufacturingProcessTemplateStageId) &&
                targetStageBySourceId.ContainsKey(
                    transition.ToManufacturingProcessTemplateStageId))
            .OrderBy(transition => transition.SequenceNo)
            .Select(transition => ManufacturingProcessTemplateSnapshotFactory.CreateTransition(
                version.BomVersionId,
                transition,
                targetStageBySourceId[transition.FromManufacturingProcessTemplateStageId]
                    .ManufacturingBomStageId,
                targetStageBySourceId[transition.ToManufacturingProcessTemplateStageId]
                    .ManufacturingBomStageId))
            .ToList();

        if (!command.IsPreview)
        {
            _db.ManufacturingBomStageMachines.RemoveRange(
                version.ManufacturingStages.SelectMany(stage => stage.Machines));
            _db.ManufacturingBomStages.RemoveRange(version.ManufacturingStages);
            await _db.ManufacturingBomStages.AddRangeAsync(stages, cancellationToken);
            await _db.ManufacturingBomStageTransitions.AddRangeAsync(transitions, cancellationToken);
            _db.AuditLogs.Add(BomAudit.Create(
                companyId,
                _currentUser.EmployeeId!.Value,
                "bom_versions",
                version.BomVersionId,
                "ApplyManufacturingProcessTemplate",
                new
                {
                    template.ManufacturingProcessTemplateId,
                    template.ExternalId,
                    template.VersionNo,
                    StageCount = stages.Count,
                    TransitionCount = transitions.Count
                },
                actionType: AuditActionType.Update));
            await _db.SaveChangesAsync(cancellationToken);
        }

        return OperationResult<ManufacturingProcessTemplateApplicationDto>.Ok(
            CreateApplicationDto(version, template, stages, transitions, hasDependencies, command.IsPreview));
    }

    private static ManufacturingProcessTemplateApplicationDto CreateApplicationDto(
        BomVersion version,
        ManufacturingProcessTemplate template,
        IReadOnlyList<ManufacturingBomStage> stages,
        IReadOnlyList<ManufacturingBomStageTransition> transitions,
        bool hasDependencies,
        bool isPreview)
    {
        return new ManufacturingProcessTemplateApplicationDto
        {
            BomVersionId = version.BomVersionId,
            ProcessTemplateId = template.ManufacturingProcessTemplateId,
            ProcessTemplateExternalId = template.ExternalId,
            ProcessTemplateVersionNo = template.VersionNo,
            IsPreview = isPreview,
            CanApply = !hasDependencies,
            BlockingReasons = hasDependencies
                ?
                [
                    new ManufacturingProcessApplicationIssueDto
                    {
                        Code = "STAGES_HAVE_DEPENDENCIES",
                        Message = "Existing stages have item assignments, transitions or loss rules."
                    }
                ]
                : [],
            Stages = stages.Select(x => new ManufacturingProcessApplicationStageDto
            {
                ExternalId = x.ExternalId,
                Name = x.Name,
                SequenceNo = x.SequenceNo,
                WorkInstruction = Map(x.WorkInstruction, isPreview),
                Machines = x.Machines.OrderBy(machine => machine.SequenceNo).Select(machine => new ManufacturingBomStageMachineDto
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
                }).ToList()
            }).ToList(),
            StageTransitions = transitions.Select(x => new ManufacturingBomStageTransitionDto
            {
                ManufacturingBomStageTransitionId = x.ManufacturingBomStageTransitionId,
                Code = x.ExternalId,
                FromStageCode = stages.Single(stage =>
                    stage.ManufacturingBomStageId == x.FromManufacturingBomStageId).ExternalId,
                ToStageCode = stages.Single(stage =>
                    stage.ManufacturingBomStageId == x.ToManufacturingBomStageId).ExternalId,
                TransitionType = x.TransitionType,
                DefaultEventCount = x.DefaultEventCount,
                SequenceNo = x.SequenceNo,
                Note = x.Note
            }).ToList()
        };
    }

    private static ManufacturingBomStageWorkInstructionDto? Map(
        ManufacturingBomStageWorkInstruction? workInstruction,
        bool preview)
    {
        if (workInstruction is null)
        {
            return null;
        }

        return new ManufacturingBomStageWorkInstructionDto
        {
            ManufacturingBomStageWorkInstructionId = preview
                ? null
                : workInstruction.ManufacturingBomStageWorkInstructionId,
            SourceWorkInstructionTemplateId = workInstruction.SourceWorkInstructionTemplateId,
            ExternalIdSnapshot = workInstruction.ExternalIdSnapshot,
            NameSnapshot = workInstruction.NameSnapshot,
            VersionNoSnapshot = workInstruction.VersionNoSnapshot,
            PurposeSnapshot = workInstruction.PurposeSnapshot,
            PreparationSnapshot = workInstruction.PreparationSnapshot,
            ProcedureSnapshot = workInstruction.ProcedureSnapshot,
            QualityRequirementsSnapshot = workInstruction.QualityRequirementsSnapshot,
            SafetyNotesSnapshot = workInstruction.SafetyNotesSnapshot,
            ChecklistItems = workInstruction.ChecklistItems
                .OrderBy(item => item.SequenceNo)
                .Select(item => new ManufacturingBomStageChecklistItemDto
                {
                    ManufacturingBomStageChecklistItemId = preview
                        ? null
                        : item.ManufacturingBomStageChecklistItemId,
                    ExternalIdSnapshot = item.ExternalIdSnapshot,
                    ContentSnapshot = item.ContentSnapshot,
                    SequenceNo = item.SequenceNo,
                    IsRequired = item.IsRequired,
                    ExpectedValueSnapshot = item.ExpectedValueSnapshot,
                    UnitSnapshot = item.UnitSnapshot
                })
                .ToList()
        };
    }
}
