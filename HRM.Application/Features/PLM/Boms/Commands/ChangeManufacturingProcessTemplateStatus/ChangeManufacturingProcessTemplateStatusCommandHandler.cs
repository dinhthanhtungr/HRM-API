using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Application.Features.PLM.Boms.Mappers;
using HRM.Application.Features.PLM.Boms.Services;
using HRM.Application.Features.PLM.Boms.Rules;
using HRM.Domain.Entities.BomSchema;
using HRM.Domain.Enums.Audits;
using HRM.Domain.Enums.Boms;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Boms.Commands.ChangeManufacturingProcessTemplateStatus;

internal sealed class ChangeManufacturingProcessTemplateStatusCommandHandler
    : IRequestHandler<
        ChangeManufacturingProcessTemplateStatusCommand,
        OperationResult<ManufacturingProcessTemplateDto>>
{
    private readonly IPLMWriteDbContext _db;
    private readonly ICurrentUser _currentUser;

    public ChangeManufacturingProcessTemplateStatusCommandHandler(
        IPLMWriteDbContext db,
        ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<OperationResult<ManufacturingProcessTemplateDto>> Handle(
        ChangeManufacturingProcessTemplateStatusCommand command,
        CancellationToken cancellationToken)
    {
        if (_currentUser.CompanyId is not { } companyId ||
            _currentUser.EmployeeId is not { } employeeId)
        {
            return OperationResult<ManufacturingProcessTemplateDto>.Fail(
                "Current company or employee is invalid.");
        }

        var entity = await _db.ManufacturingProcessTemplates
            .AsSplitQuery()
            .Include(x => x.ApplicabilityRules).ThenInclude(x => x.Category)
            .Include(x => x.Stages)
                .ThenInclude(x => x.WorkInstructionTemplate)
            .Include(x => x.Stages).ThenInclude(x => x.Machines).ThenInclude(x => x.Equipment)
            .Include(x => x.Stages).ThenInclude(x => x.Machines).ThenInclude(x => x.Parameters)
            .Include(x => x.StageTransitions).ThenInclude(x => x.FromStage)
            .Include(x => x.StageTransitions).ThenInclude(x => x.ToStage)
            .FirstOrDefaultAsync(
                template => template.ManufacturingProcessTemplateId == command.TemplateId &&
                            template.CompanyId == companyId,
                cancellationToken);
        if (entity is null)
        {
            return OperationResult<ManufacturingProcessTemplateDto>.Fail(
                "Process template was not found.");
        }

        if (command.Action == ManufacturingTemplateLifecycleAction.Release)
        {
            var releasedAt = DateTime.Now;
            var sharedDraftInstructionIds = await GetSharedDraftWorkInstructionIds(
                entity,
                companyId,
                cancellationToken);
            var issues = ManufacturingProcessTemplateReleaseValidator.Validate(
                entity,
                releasedAt,
                sharedDraftInstructionIds);
            if (issues.Count > 0)
            {
                return OperationResult<ManufacturingProcessTemplateDto>.Fail(issues[0].Message);
            }

            ReleaseLinkedDraftWorkInstructions(entity, companyId, employeeId, releasedAt);
            entity.Status = ManufacturingTemplateStatus.Released;
            entity.ReleasedDate = releasedAt;
            entity.ReleasedBy = employeeId;
        }
        else
        {
            if (entity.Status != ManufacturingTemplateStatus.Released)
            {
                return OperationResult<ManufacturingProcessTemplateDto>.Fail(
                    "Only Released process templates can be made obsolete.");
            }

            entity.Status = ManufacturingTemplateStatus.Obsolete;
        }

        entity.UpdatedDate = DateTime.Now;
        entity.UpdatedBy = employeeId;
        _db.AuditLogs.Add(BomAudit.Create(
            companyId,
            employeeId,
            "manufacturing_process_templates",
            entity.ManufacturingProcessTemplateId,
            command.Action == ManufacturingTemplateLifecycleAction.Release
                ? "ReleaseManufacturingProcessTemplate"
                : "ObsoleteManufacturingProcessTemplate",
            new { entity.ExternalId, entity.VersionNo, entity.Status },
            actionType: AuditActionType.Update));
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<ManufacturingProcessTemplateDto>.Ok(
            ManufacturingTemplateMapper.ToDto(entity));
    }

    private void ReleaseLinkedDraftWorkInstructions(
        ManufacturingProcessTemplate processTemplate,
        Guid companyId,
        Guid employeeId,
        DateTime releasedAt)
    {
        var draftInstructions = processTemplate.Stages
            .Select(stage => stage.WorkInstructionTemplate)
            .Where(instruction => instruction?.Status == ManufacturingTemplateStatus.Draft)
            .DistinctBy(instruction => instruction!.ManufacturingWorkInstructionTemplateId)
            .Select(instruction => instruction!)
            .ToList();

        foreach (var instruction in draftInstructions)
        {
            instruction.Status = ManufacturingTemplateStatus.Released;
            instruction.ReleasedDate = releasedAt;
            instruction.ReleasedBy = employeeId;
            instruction.UpdatedDate = releasedAt;
            instruction.UpdatedBy = employeeId;

            _db.AuditLogs.Add(BomAudit.Create(
                companyId,
                employeeId,
                "manufacturing_work_instruction_templates",
                instruction.ManufacturingWorkInstructionTemplateId,
                "ReleaseWorkInstructionTemplateWithProcessTemplate",
                new
                {
                    instruction.ExternalId,
                    instruction.VersionNo,
                    instruction.Status,
                    processTemplate.ManufacturingProcessTemplateId
                },
                actionType: AuditActionType.Update));
        }
    }

    private async Task<HashSet<Guid>> GetSharedDraftWorkInstructionIds(
        ManufacturingProcessTemplate processTemplate,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var draftInstructionIds = processTemplate.Stages
            .Select(stage => stage.WorkInstructionTemplate)
            .Where(instruction => instruction?.Status == ManufacturingTemplateStatus.Draft)
            .Select(instruction => instruction!.ManufacturingWorkInstructionTemplateId)
            .Distinct()
            .ToList();
        if (draftInstructionIds.Count == 0)
            return [];

        return await _db.ManufacturingProcessTemplateStages
            .AsNoTracking()
            .Where(stage =>
                stage.ManufacturingWorkInstructionTemplateId.HasValue &&
                draftInstructionIds.Contains(stage.ManufacturingWorkInstructionTemplateId.Value) &&
                stage.ManufacturingProcessTemplateId != processTemplate.ManufacturingProcessTemplateId &&
                stage.ProcessTemplate.CompanyId == companyId)
            .Select(stage => stage.ManufacturingWorkInstructionTemplateId!.Value)
            .Distinct()
            .ToHashSetAsync(cancellationToken);
    }
}
