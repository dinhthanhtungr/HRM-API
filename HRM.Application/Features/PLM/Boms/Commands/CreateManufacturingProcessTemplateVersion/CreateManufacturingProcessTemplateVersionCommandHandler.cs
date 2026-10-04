using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Application.Features.PLM.Boms.Mappers;
using HRM.Application.Features.PLM.Boms.Services;
using HRM.Domain.Entities.BomSchema;
using HRM.Domain.Enums.Audits;
using HRM.Domain.Enums.Boms;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Boms.Commands.CreateManufacturingProcessTemplateVersion;

internal sealed class CreateManufacturingProcessTemplateVersionCommandHandler
    : IRequestHandler<
        CreateManufacturingProcessTemplateVersionCommand,
        OperationResult<ManufacturingProcessTemplateDto>>
{
    private readonly IPLMWriteDbContext _db;
    private readonly ICurrentUser _currentUser;

    public CreateManufacturingProcessTemplateVersionCommandHandler(
        IPLMWriteDbContext db,
        ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<OperationResult<ManufacturingProcessTemplateDto>> Handle(
        CreateManufacturingProcessTemplateVersionCommand command,
        CancellationToken cancellationToken)
    {
        if (_currentUser.CompanyId is not { } companyId ||
            _currentUser.EmployeeId is not { } employeeId)
        {
            return OperationResult<ManufacturingProcessTemplateDto>.Fail(
                "Current company or employee is invalid.");
        }

        // Load the complete source graph so the new Draft is a faithful clone.
        var source = await _db.ManufacturingProcessTemplates
            .AsNoTracking()
            .Include(x => x.ApplicabilityRules)
            .Include(x => x.Stages)
                .ThenInclude(x => x.WorkInstructionTemplate)
            .Include(x => x.Stages)
                .ThenInclude(x => x.Machines)
                .ThenInclude(x => x.Equipment)
            .Include(x => x.Stages)
                .ThenInclude(x => x.Machines)
                .ThenInclude(x => x.Parameters)
            .Include(x => x.StageTransitions)
            .FirstOrDefaultAsync(
                template => template.ManufacturingProcessTemplateId == command.SourceTemplateId &&
                            template.CompanyId == companyId &&
                            template.Status != ManufacturingTemplateStatus.Draft,
                cancellationToken);

        if (source is null)
        {
            return OperationResult<ManufacturingProcessTemplateDto>.Fail(
                "A Released or Obsolete source process template was not found.");
        }

        var versionNo = await _db.ManufacturingProcessTemplates
            .Where(template =>
                template.CompanyId == companyId &&
                template.ExternalId == source.ExternalId)
            .MaxAsync(template => template.VersionNo, cancellationToken) + 1;

        // Clone applicability, stages, machines, parameters and transitions with new IDs.
        var entity = new ManufacturingProcessTemplate
        {
            ManufacturingProcessTemplateId = Guid.CreateVersion7(),
            CompanyId = companyId,
            ExternalId = source.ExternalId,
            Name = source.Name,
            Description = source.Description,
            VersionNo = versionNo,
            EffectiveFrom = source.EffectiveFrom,
            EffectiveTo = source.EffectiveTo,
            CreatedDate = DateTime.Now,
            CreatedBy = employeeId,
            ApplicabilityRules = source.ApplicabilityRules.Select(x => new ManufacturingProcessTemplateApplicability
            {
                ManufacturingProcessTemplateApplicabilityId = Guid.CreateVersion7(),
                CategoryId = x.CategoryId,
                StepOfProduct = x.StepOfProduct,
                Priority = x.Priority,
                Note = x.Note
            }).ToList(),
            Stages = source.Stages.Where(x => x.IsActive).Select(x => new ManufacturingProcessTemplateStage
            {
                ManufacturingProcessTemplateStageId = Guid.CreateVersion7(),
                ExternalId = x.ExternalId,
                Code = x.Code,
                Name = x.Name,
                Description = x.Description,
                SequenceNo = x.SequenceNo,
                ManufacturingWorkInstructionTemplateId = x.ManufacturingWorkInstructionTemplateId,

                Machines = x.Machines.Select(m => new ManufacturingProcessTemplateStageMachine
                {
                    ManufacturingProcessTemplateStageMachineId = Guid.CreateVersion7(),
                    EquipmentId = m.EquipmentId,
                    ConfigurationGroupKey = m.ConfigurationGroupKey,
                    ConfigurationGroupName = m.ConfigurationGroupName,
                    IsDefault = m.IsDefault,
                    SequenceNo = m.SequenceNo,
                    Note = m.Note,
                    Parameters = m.Parameters.Select(parameter => new ManufacturingProcessTemplateStageMachineParameter
                    {
                        ManufacturingProcessTemplateStageMachineParameterId = Guid.CreateVersion7(),
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
            }).ToList()
        };
        var clonedStageBySourceId = source.Stages
            .Where(stage => stage.IsActive)
            .ToDictionary(
                stage => stage.ManufacturingProcessTemplateStageId,
                stage => entity.Stages.Single(clone =>
                    string.Equals(
                        clone.ExternalId,
                        stage.ExternalId,
                        StringComparison.OrdinalIgnoreCase)));
        entity.StageTransitions = source.StageTransitions
            .Where(transition =>
                clonedStageBySourceId.ContainsKey(
                    transition.FromManufacturingProcessTemplateStageId) &&
                clonedStageBySourceId.ContainsKey(
                    transition.ToManufacturingProcessTemplateStageId))
            .Select(x => new ManufacturingProcessTemplateStageTransition
            {
                ManufacturingProcessTemplateStageTransitionId = Guid.CreateVersion7(),
                ExternalId = x.ExternalId,
                Code = x.Code,
                FromManufacturingProcessTemplateStageId =
                    clonedStageBySourceId[x.FromManufacturingProcessTemplateStageId]
                        .ManufacturingProcessTemplateStageId,
                ToManufacturingProcessTemplateStageId =
                    clonedStageBySourceId[x.ToManufacturingProcessTemplateStageId]
                        .ManufacturingProcessTemplateStageId,
                TransitionType = x.TransitionType,
                DefaultEventCount = x.DefaultEventCount,
                SequenceNo = x.SequenceNo,
                Note = x.Note
            }).ToList();
        await _db.ManufacturingProcessTemplates.AddAsync(entity, cancellationToken);
        _db.AuditLogs.Add(BomAudit.Create(
            companyId,
            employeeId,
            "manufacturing_process_templates",
            entity.ManufacturingProcessTemplateId,
            "CreateManufacturingProcessTemplateVersion",
            new
            {
                SourceTemplateId = source.ManufacturingProcessTemplateId,
                entity.ExternalId,
                entity.VersionNo,
                command.ChangeReason
            },
            actionType: AuditActionType.Create));
        await _db.SaveChangesAsync(cancellationToken);

        var response = await _db.ManufacturingProcessTemplates
            .AsNoTracking()
            .Include(x => x.ApplicabilityRules).ThenInclude(x => x.Category)
            .Include(x => x.Stages).ThenInclude(x => x.WorkInstructionTemplate)
            .Include(x => x.Stages).ThenInclude(x => x.Machines).ThenInclude(x => x.Equipment)
            .Include(x => x.Stages).ThenInclude(x => x.Machines).ThenInclude(x => x.Parameters)
            .Include(x => x.StageTransitions).ThenInclude(x => x.FromStage)
            .Include(x => x.StageTransitions).ThenInclude(x => x.ToStage)
            .FirstAsync(
                template => template.ManufacturingProcessTemplateId ==
                            entity.ManufacturingProcessTemplateId,
                cancellationToken);
        return OperationResult<ManufacturingProcessTemplateDto>.Ok(
            ManufacturingTemplateMapper.ToDto(response));
    }
}
