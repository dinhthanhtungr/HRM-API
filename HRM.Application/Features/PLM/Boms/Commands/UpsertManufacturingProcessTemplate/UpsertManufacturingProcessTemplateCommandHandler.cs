using HRM.Application.Abstractions.Commons.ExternalIds;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Concurrency;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Application.Features.PLM.Boms.Mappers;
using HRM.Application.Features.PLM.Boms.Rules;
using HRM.Application.Features.PLM.Boms.Services;
using HRM.Domain.Entities.BomSchema;
using HRM.Domain.Enums.Audits;
using HRM.Domain.Enums.Boms;
using HRM.Domain.Enums.Category;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Boms.Commands.UpsertManufacturingProcessTemplate;

internal sealed class UpsertManufacturingProcessTemplateCommandHandler
    : IRequestHandler<
        UpsertManufacturingProcessTemplateCommand,
        OperationResult<ManufacturingProcessTemplateEditorDto>>
{
    private readonly IPLMWriteDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IExternalIdService _externalIdService;

    public UpsertManufacturingProcessTemplateCommandHandler(
        IPLMWriteDbContext db,
        ICurrentUser currentUser,
        IExternalIdService externalIdService)
    {
        _db = db;
        _currentUser = currentUser;
        _externalIdService = externalIdService;
    }

    public async Task<OperationResult<ManufacturingProcessTemplateEditorDto>> Handle(UpsertManufacturingProcessTemplateCommand command, CancellationToken cancellationToken)
    {
        // Resolve tenant and actor before reading or changing the aggregate.
        if (_currentUser.CompanyId is not { } companyId ||
            companyId == Guid.Empty ||
            _currentUser.EmployeeId is not { } employeeId ||
            employeeId == Guid.Empty)
        {
            return OperationResult<ManufacturingProcessTemplateEditorDto>.Fail(
                "Current company or employee is invalid.");
        }

        if (!ManufacturingProcessTemplateMachineGroupPayloadNormalizer.TryExpand(command.Request, out var request, out var payloadError))
        {
            return OperationResult<ManufacturingProcessTemplateEditorDto>.Fail(payloadError!);
        }

        // Validate the complete replacement payload before opening a transaction.
        var savedAt = DateTime.Now;
        var effectiveFrom = request.EffectiveFrom ?? savedAt;

        if (string.IsNullOrWhiteSpace(request.Name) ||
            request.Name.Trim().Length > 200 ||
            request.Stages.Count == 0)
        {
            return OperationResult<ManufacturingProcessTemplateEditorDto>.Fail(
                "Name is required, must not exceed 200 characters, and at least one stage is required.");
        }

        if (request.EffectiveTo.HasValue && request.EffectiveTo.Value < effectiveFrom)
        {
            return OperationResult<ManufacturingProcessTemplateEditorDto>.Fail(
                "EffectiveTo must not be earlier than EffectiveFrom.");
        }

        var hasInvalidStage = request.Stages.Any(stage =>
            string.IsNullOrWhiteSpace(stage.Code) ||
            stage.Code.Trim().Length > 64 ||
            string.IsNullOrWhiteSpace(stage.Name) ||
            stage.Name.Trim().Length > 200 ||
            stage.SequenceNo <= 0);
        var hasDuplicateStageSequence = request.Stages
            .GroupBy(stage => stage.SequenceNo)
            .Any(group => group.Count() > 1);
        var hasDuplicateStageCode = request.Stages
            .GroupBy(stage => stage.Code.Trim(), StringComparer.OrdinalIgnoreCase)
            .Any(group => group.Count() > 1);

        if (hasInvalidStage || hasDuplicateStageSequence || hasDuplicateStageCode)
        {
            return OperationResult<ManufacturingProcessTemplateEditorDto>.Fail(
                "Stage Code, Name and SequenceNo must be valid; Code and SequenceNo must be unique; Name must not exceed 200 characters.");
        }

        if (ValidateApplicabilityRules(request.ApplicabilityRules) is { } applicabilityError)
        {
            return OperationResult<ManufacturingProcessTemplateEditorDto>.Fail(applicabilityError);
        }

        var submittedStageIds = request.Stages
            .Where(stage => stage.ManufacturingProcessTemplateStageId.HasValue)
            .Select(stage => stage.ManufacturingProcessTemplateStageId!.Value)
            .ToList();
        if (submittedStageIds.Count != submittedStageIds.Distinct().Count())
        {
            return OperationResult<ManufacturingProcessTemplateEditorDto>.Fail(
                "Stage IDs must be unique.");
        }

        var hasInvalidMachineList = request.Stages.Any(stage =>
            stage.Machines.Any(machine => machine.EquipmentId <= 0 || machine.SequenceNo <= 0) ||
            stage.Machines.GroupBy(machine => machine.EquipmentId).Any(group => group.Count() > 1) ||
            stage.Machines.GroupBy(machine => machine.SequenceNo).Any(group => group.Count() > 1) ||
            stage.Machines.Count > 0 && stage.Machines.Count(machine => machine.IsDefault) != 1);
        if (hasInvalidMachineList)
        {
            return OperationResult<ManufacturingProcessTemplateEditorDto>.Fail(
                "Each stage machine list must have unique EquipmentId and SequenceNo, and exactly one default when machines are configured.");
        }

        if (ValidateMachineParameters(request.Stages) is { } parameterError)
        {
            return OperationResult<ManufacturingProcessTemplateEditorDto>.Fail(parameterError);
        }

        if (ValidateMachineConfigurationGroups(request.Stages) is { } groupError)
        {
            return OperationResult<ManufacturingProcessTemplateEditorDto>.Fail(groupError);
        }

        if (ValidateStageTransitions(request) is { } transitionError)
        {
            return OperationResult<ManufacturingProcessTemplateEditorDto>.Fail(transitionError);
        }

        // Validate every referenced record inside the current company boundary.
        var instructionIds = request.Stages
            .Where(stage => stage.ManufacturingWorkInstructionTemplateId.HasValue)
            .Select(stage => stage.ManufacturingWorkInstructionTemplateId!.Value)
            .Distinct()
            .ToList();
        var validInstructions = await _db.ManufacturingWorkInstructionTemplates.AsNoTracking()
            .Where(x => instructionIds.Contains(x.ManufacturingWorkInstructionTemplateId) &&
                        x.CompanyId == companyId &&
                        x.IsActive &&
                        (x.Status == ManufacturingTemplateStatus.Draft ||
                         x.Status == ManufacturingTemplateStatus.Released))
            .Select(x => x.ManufacturingWorkInstructionTemplateId)
            .ToListAsync(cancellationToken);
        if (validInstructions.Count != instructionIds.Count)
        {
            return OperationResult<ManufacturingProcessTemplateEditorDto>.Fail(
                "Every Work Instruction must be Draft or Released, active and belong to the current company.");
        }

        var equipmentIds = request.Stages
            .SelectMany(stage => stage.Machines)
            .Select(machine => machine.EquipmentId)
            .Distinct()
            .ToList();
        var validEquipmentIds = await _db.EquipmentsMro.AsNoTracking()
            .Where(x => equipmentIds.Contains(x.EquipmentId) && x.FactoryId == companyId)
            .Select(x => x.EquipmentId)
            .ToListAsync(cancellationToken);
        if (validEquipmentIds.Count != equipmentIds.Count)
        {
            return OperationResult<ManufacturingProcessTemplateEditorDto>.Fail(
                "Every machine must belong to the current company.");
        }

        var categoryIds = request.ApplicabilityRules
            .Where(rule => rule.CategoryId.HasValue)
            .Select(rule => rule.CategoryId!.Value)
            .Distinct()
            .ToList();
        var validCategoryIds = await _db.Categories.AsNoTracking()
            .Where(x => categoryIds.Contains(x.CategoryId) && x.CompanyId == companyId && x.IsActive == true)
            .Select(x => x.CategoryId)
            .ToListAsync(cancellationToken);
        if (validCategoryIds.Count != categoryIds.Count)
        {
            return OperationResult<ManufacturingProcessTemplateEditorDto>.Fail(
                "Every applicability category must be active and belong to the current company.");
        }

        // Load the existing Draft or initialize a new aggregate.
        ManufacturingProcessTemplate entity;
        var isCreate = !command.TemplateId.HasValue;
        if (command.TemplateId is { } id)
        {
            entity = await _db.ManufacturingProcessTemplates
                .Include(x => x.Stages).ThenInclude(x => x.Machines).ThenInclude(x => x.Parameters)
                .Include(x => x.StageTransitions)
                .Include(x => x.ApplicabilityRules)
                .FirstOrDefaultAsync(
                    template => template.ManufacturingProcessTemplateId == id &&
                                template.CompanyId == companyId,
                    cancellationToken) ?? null!;
            if (entity is null)
            {
                return OperationResult<ManufacturingProcessTemplateEditorDto>.Fail(
                    "Process template was not found.");
            }

            if (entity.Status != ManufacturingTemplateStatus.Draft)
            {
                return OperationResult<ManufacturingProcessTemplateEditorDto>.Fail(
                    "Only Draft process templates can be changed.");
            }

            if (submittedStageIds.Except(
                    entity.Stages.Select(stage => stage.ManufacturingProcessTemplateStageId)).Any())
            {
                return OperationResult<ManufacturingProcessTemplateEditorDto>.Fail(
                    "A stage does not belong to this process template.");
            }

            if (entity.UpdatedDate.HasValue && !request.ExpectedUpdatedDate.HasValue)
            {
                return OperationResult<ManufacturingProcessTemplateEditorDto>.Fail(
                    OptimisticConcurrencyHelper.CreateConflictMessage(
                        "Manufacturing process template"));
            }

            if (OptimisticConcurrencyHelper.ValidateExpectedUpdatedDateWithDatabasePrecision(
                    request.ExpectedUpdatedDate,
                    entity.UpdatedDate,
                    "Manufacturing process template") is { } conflict)
            {
                return OperationResult<ManufacturingProcessTemplateEditorDto>.Fail(conflict);
            }

            var submittedTransitionIds = request.StageTransitions
                .Where(x => x.ManufacturingProcessTemplateStageTransitionId.HasValue)
                .Select(x => x.ManufacturingProcessTemplateStageTransitionId!.Value)
                .ToList();
            if (submittedTransitionIds.Count != submittedTransitionIds.Distinct().Count())
                return OperationResult<ManufacturingProcessTemplateEditorDto>.Fail("Stage transition IDs must be unique.");
            if (submittedTransitionIds.Except(
                    entity.StageTransitions.Select(
                        transition => transition.ManufacturingProcessTemplateStageTransitionId)).Any())
            {
                return OperationResult<ManufacturingProcessTemplateEditorDto>.Fail(
                    "A stage transition does not belong to this process template.");
            }
        }
        else
        {
            if (submittedStageIds.Count > 0 ||
                request.StageTransitions.Any(
                    transition => transition.ManufacturingProcessTemplateStageTransitionId.HasValue))
            {
                return OperationResult<ManufacturingProcessTemplateEditorDto>.Fail(
                    "New process templates cannot reference existing stage or transition IDs.");
            }

            entity = new ManufacturingProcessTemplate
            {
                ManufacturingProcessTemplateId = Guid.CreateVersion7(),
                CompanyId = companyId,
                ExternalId = await _externalIdService.GenerateGlobalCodeAsync(
                    companyId,
                    DocumentPrefix.MPT.ToString(),
                    cancellationToken),
                VersionNo = 1,
                CreatedDate = savedAt,
                CreatedBy = employeeId
            };
            await _db.ManufacturingProcessTemplates.AddAsync(entity, cancellationToken);
        }

        // Stage sequence values temporarily when unique indexes would otherwise collide.
        await using var transaction = await _db.BeginTransactionAsync(cancellationToken);
        if (!isCreate)
        {
            try
            {
                var stagingPlan = CreateSequenceStagingPlan(entity, request);
                if (stagingPlan.HasChanges)
                    await MoveExistingSequenceNumbersOutOfTheWayAsync(entity, request, stagingPlan, cancellationToken);
            }
            catch (DbUpdateConcurrencyException exception)
            {
                await transaction.RollbackAsync(cancellationToken);
                return OperationResult<ManufacturingProcessTemplateEditorDto>.Fail(
                    OptimisticConcurrencyHelper.CreateConflictMessage("Manufacturing process template", exception));
            }
        }

        // Apply the submitted aggregate. Missing children are intentionally removed.
        entity.Name = request.Name.Trim();
        entity.Description = Normalize(request.Description);
        entity.EffectiveFrom = effectiveFrom;
        entity.EffectiveTo = request.EffectiveTo;
        if (!isCreate)
        {
            entity.UpdatedDate = savedAt;
            entity.UpdatedBy = employeeId;
        }

        _db.ManufacturingProcessTemplateApplicabilities.RemoveRange(entity.ApplicabilityRules);
        var updatedApplicabilityRules = request.ApplicabilityRules
            .Select(rule => new ManufacturingProcessTemplateApplicability
            {
                ManufacturingProcessTemplateApplicabilityId = Guid.CreateVersion7(),
                ManufacturingProcessTemplateId = entity.ManufacturingProcessTemplateId,
                CategoryId = rule.CategoryId,
                StepOfProduct = rule.StepOfProduct,
                Priority = rule.Priority,
                Note = Normalize(rule.Note)
            })
            .ToList();
        _db.ManufacturingProcessTemplateApplicabilities.AddRange(updatedApplicabilityRules);
        entity.ApplicabilityRules = updatedApplicabilityRules;

        var existingById = entity.Stages.ToDictionary(x => x.ManufacturingProcessTemplateStageId);
        var removedStages = entity.Stages
            .Where(stage => !submittedStageIds.Contains(stage.ManufacturingProcessTemplateStageId))
            .ToList();
        var updatedStages = new List<ManufacturingProcessTemplateStage>();

        foreach (var source in request.Stages)
        {
            ManufacturingProcessTemplateStage stage;
            if (source.ManufacturingProcessTemplateStageId is { } stageId)
            {
                stage = existingById[stageId];
            }
            else
            {
                stage = new ManufacturingProcessTemplateStage
                {
                    ManufacturingProcessTemplateStageId = Guid.CreateVersion7(),
                    ManufacturingProcessTemplateId = entity.ManufacturingProcessTemplateId,
                    ExternalId = await _externalIdService.GenerateGlobalCodeAsync(
                        companyId,
                        DocumentPrefix.MPS.ToString(),
                        cancellationToken)
                };
                _db.ManufacturingProcessTemplateStages.Add(stage);
            }

            stage.Code = source.Code.Trim().ToUpperInvariant();
            stage.Name = source.Name.Trim();
            stage.SequenceNo = source.SequenceNo;
            stage.Description = Normalize(source.Description);
            stage.ManufacturingWorkInstructionTemplateId =
                source.ManufacturingWorkInstructionTemplateId;
            ApplyMachines(stage, source.Machines);
            updatedStages.Add(stage);
        }

        var stagesByCode = updatedStages.ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);
        var existingTransitionsById = entity.StageTransitions.ToDictionary(
            transition => transition.ManufacturingProcessTemplateStageTransitionId);
        var existingTransitionsByCode = entity.StageTransitions.ToDictionary(
            transition => transition.Code,
            StringComparer.OrdinalIgnoreCase);
        var requestedTransitionIds = request.StageTransitions
            .Select(transition => transition.ManufacturingProcessTemplateStageTransitionId ??
                (existingTransitionsByCode.TryGetValue(transition.Code.Trim(), out var existing)
                    ? existing.ManufacturingProcessTemplateStageTransitionId
                    : (Guid?)null))
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .ToHashSet();
        _db.ManufacturingProcessTemplateStageTransitions.RemoveRange(
            entity.StageTransitions.Where(
                transition => !requestedTransitionIds.Contains(
                    transition.ManufacturingProcessTemplateStageTransitionId)));

        var updatedTransitions = new List<ManufacturingProcessTemplateStageTransition>();
        foreach (var source in request.StageTransitions)
        {
            ManufacturingProcessTemplateStageTransition transition;
            if (source.ManufacturingProcessTemplateStageTransitionId is { } transitionId)
            {
                transition = existingTransitionsById[transitionId];
            }
            else if (existingTransitionsByCode.TryGetValue(source.Code.Trim(), out var existingTransition))
            {
                transition = existingTransition;
            }
            else
            {
                transition = new ManufacturingProcessTemplateStageTransition
                {
                    ManufacturingProcessTemplateStageTransitionId = Guid.CreateVersion7(),
                    ManufacturingProcessTemplateId = entity.ManufacturingProcessTemplateId,
                    ExternalId = await _externalIdService.GenerateGlobalCodeAsync(
                        companyId,
                        DocumentPrefix.MPTT.ToString(),
                        cancellationToken)
                };
                _db.ManufacturingProcessTemplateStageTransitions.Add(transition);
            }

            transition.FromManufacturingProcessTemplateStageId =
                stagesByCode[source.FromStageCode.Trim()].ManufacturingProcessTemplateStageId;
            transition.ToManufacturingProcessTemplateStageId =
                stagesByCode[source.ToStageCode.Trim()].ManufacturingProcessTemplateStageId;
            transition.Code = source.Code.Trim().ToUpperInvariant();
            transition.TransitionType = source.TransitionType;
            transition.DefaultEventCount = source.DefaultEventCount;
            transition.SequenceNo = source.SequenceNo;
            transition.Note = Normalize(source.Note);
            updatedTransitions.Add(transition);
        }
        entity.StageTransitions = updatedTransitions;
        _db.ManufacturingProcessTemplateStages.RemoveRange(removedStages);
        entity.Stages = updatedStages;

        _db.AuditLogs.Add(BomAudit.Create(
            companyId,
            employeeId,
            "manufacturing_process_templates",
            entity.ManufacturingProcessTemplateId,
            isCreate
                ? "CreateManufacturingProcessTemplate"
                : "UpdateManufacturingProcessTemplate",
            new
            {
                entity.ExternalId,
                entity.VersionNo,
                StageCount = entity.Stages.Count,
                TransitionCount = entity.StageTransitions.Count,
                ApplicabilityRuleCount = entity.ApplicabilityRules.Count
            },
            actionType: isCreate ? AuditActionType.Create : AuditActionType.Update));

        // Persist all child changes atomically, then return the canonical editor shape.
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            return OperationResult<ManufacturingProcessTemplateEditorDto>.Fail(
                OptimisticConcurrencyHelper.CreateConflictMessage(
                    "Manufacturing process template",
                    exception));
        }

        var responseEntity = await _db.ManufacturingProcessTemplates
            .AsNoTracking()
            .AsSplitQuery()
            .Include(template => template.ApplicabilityRules)
                .ThenInclude(rule => rule.Category)
            .Include(template => template.Stages)
                .ThenInclude(stage => stage.WorkInstructionTemplate)
            .Include(template => template.Stages)
                .ThenInclude(stage => stage.Machines)
                .ThenInclude(machine => machine.Equipment)
            .Include(template => template.Stages)
                .ThenInclude(stage => stage.Machines)
                .ThenInclude(machine => machine.Parameters)
            .Include(template => template.StageTransitions)
            .FirstAsync(
                template => template.ManufacturingProcessTemplateId ==
                            entity.ManufacturingProcessTemplateId,
                cancellationToken);

        return OperationResult<ManufacturingProcessTemplateEditorDto>.Ok(
            ManufacturingTemplateMapper.ToEditorDto(responseEntity));
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void ApplyMachines(
        ManufacturingProcessTemplateStage stage,
        IReadOnlyList<ManufacturingProcessTemplateStageMachineWriteDto> requestedMachines)
    {
        var existingByEquipmentId = stage.Machines.ToDictionary(x => x.EquipmentId);
        var requestedEquipmentIds = requestedMachines.Select(x => x.EquipmentId).ToHashSet();
        _db.ManufacturingProcessTemplateStageMachines.RemoveRange(
            stage.Machines.Where(x => !requestedEquipmentIds.Contains(x.EquipmentId)));

        var updatedMachines = new List<ManufacturingProcessTemplateStageMachine>();
        foreach (var source in requestedMachines)
        {
            if (!existingByEquipmentId.TryGetValue(source.EquipmentId, out var machine))
            {
                machine = new ManufacturingProcessTemplateStageMachine
                {
                    ManufacturingProcessTemplateStageMachineId = Guid.CreateVersion7(),
                    ManufacturingProcessTemplateStageId = stage.ManufacturingProcessTemplateStageId,
                    EquipmentId = source.EquipmentId
                };
                _db.ManufacturingProcessTemplateStageMachines.Add(machine);
            }

            machine.ConfigurationGroupKey = source.ConfigurationGroupKey;
            machine.ConfigurationGroupName = Normalize(source.ConfigurationGroupName);
            machine.IsDefault = source.IsDefault;
            machine.SequenceNo = source.SequenceNo;
            machine.Note = Normalize(source.Note);
            ApplyParameters(machine, source.Parameters);
            updatedMachines.Add(machine);
        }

        stage.Machines = updatedMachines;
    }

    private void ApplyParameters(
        ManufacturingProcessTemplateStageMachine machine,
        IReadOnlyList<ManufacturingProcessTemplateStageMachineParameterWriteDto> requestedParameters)
    {
        var existingByCode = machine.Parameters.ToDictionary(
            parameter => parameter.ParameterCode,
            StringComparer.OrdinalIgnoreCase);
        var requestedCodes = requestedParameters
            .Select(parameter => parameter.ParameterCode.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        _db.ManufacturingProcessTemplateStageMachineParameters.RemoveRange(
            machine.Parameters.Where(x => !requestedCodes.Contains(x.ParameterCode)));

        var updatedParameters = new List<ManufacturingProcessTemplateStageMachineParameter>();
        foreach (var source in requestedParameters)
        {
            var code = source.ParameterCode.Trim().ToUpperInvariant();
            if (!existingByCode.TryGetValue(code, out var parameter))
            {
                parameter = new ManufacturingProcessTemplateStageMachineParameter
                {
                    ManufacturingProcessTemplateStageMachineParameterId = Guid.CreateVersion7(),
                    ManufacturingProcessTemplateStageMachineId = machine.ManufacturingProcessTemplateStageMachineId,
                    ParameterCode = code
                };
                _db.ManufacturingProcessTemplateStageMachineParameters.Add(parameter);
            }

            parameter.ParameterName = source.ParameterName.Trim();
            parameter.TargetValue = source.TargetValue;
            parameter.MinValue = source.MinValue;
            parameter.MaxValue = source.MaxValue;
            parameter.Unit = source.Unit.Trim();
            parameter.IsRequired = source.IsRequired;
            parameter.SequenceNo = source.SequenceNo;
            parameter.Note = Normalize(source.Note);
            updatedParameters.Add(parameter);
        }

        machine.Parameters = updatedParameters;
    }

    private async Task MoveExistingSequenceNumbersOutOfTheWayAsync(
        ManufacturingProcessTemplate entity,
        UpsertManufacturingProcessTemplateRequest request,
        SequenceStagingPlan plan,
        CancellationToken cancellationToken)
    {
        if (plan.Stages)
        {
            var stageOffset =
                entity.Stages.Select(stage => stage.SequenceNo).DefaultIfEmpty(0).Max() +
                request.Stages.Select(stage => stage.SequenceNo).DefaultIfEmpty(0).Max() +
                1;
            foreach (var stage in entity.Stages)
                stage.SequenceNo += stageOffset;
        }

        var existingMachines = entity.Stages.SelectMany(x => x.Machines).ToList();
        if (plan.Machines)
        {
            var machineOffset =
                existingMachines.Select(machine => machine.SequenceNo).DefaultIfEmpty(0).Max() +
                request.Stages
                    .SelectMany(stage => stage.Machines)
                    .Select(machine => machine.SequenceNo)
                    .DefaultIfEmpty(0)
                    .Max() +
                1;
            foreach (var machine in existingMachines)
                machine.SequenceNo += machineOffset;
        }

        var existingParameters = existingMachines.SelectMany(x => x.Parameters).ToList();
        if (plan.Parameters)
        {
            var parameterOffset =
                existingParameters.Select(parameter => parameter.SequenceNo).DefaultIfEmpty(0).Max() +
                request.Stages
                    .SelectMany(stage => stage.Machines)
                    .SelectMany(machine => machine.Parameters)
                    .Select(parameter => parameter.SequenceNo)
                    .DefaultIfEmpty(0)
                    .Max() +
                1;
            foreach (var parameter in existingParameters)
                parameter.SequenceNo += parameterOffset;
        }

        if (plan.Transitions)
        {
            var transitionOffset =
                entity.StageTransitions
                    .Select(transition => transition.SequenceNo)
                    .DefaultIfEmpty(0)
                    .Max() +
                request.StageTransitions
                    .Select(transition => transition.SequenceNo)
                    .DefaultIfEmpty(0)
                    .Max() +
                1;
            foreach (var transition in entity.StageTransitions)
                transition.SequenceNo += transitionOffset;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static SequenceStagingPlan CreateSequenceStagingPlan(
        ManufacturingProcessTemplate entity,
        UpsertManufacturingProcessTemplateRequest request)
    {
        var existingStages = entity.Stages.ToDictionary(x => x.ManufacturingProcessTemplateStageId);
        var stageChanges = request.Stages.Any(source =>
            !source.ManufacturingProcessTemplateStageId.HasValue ||
            existingStages[source.ManufacturingProcessTemplateStageId.Value].SequenceNo != source.SequenceNo);

        var machineChanges = false;
        var parameterChanges = false;
        foreach (var sourceStage in request.Stages.Where(x => x.ManufacturingProcessTemplateStageId.HasValue))
        {
            var existingStage = existingStages[sourceStage.ManufacturingProcessTemplateStageId!.Value];
            var existingMachines = existingStage.Machines.ToDictionary(x => x.EquipmentId);
            foreach (var sourceMachine in sourceStage.Machines)
            {
                if (!existingMachines.TryGetValue(sourceMachine.EquipmentId, out var existingMachine))
                {
                    machineChanges = true;
                    parameterChanges |= sourceMachine.Parameters.Count > 0;
                    continue;
                }

                machineChanges |= existingMachine.SequenceNo != sourceMachine.SequenceNo;
                var existingParameters = existingMachine.Parameters.ToDictionary(x => x.ParameterCode, StringComparer.OrdinalIgnoreCase);
                parameterChanges |= sourceMachine.Parameters.Any(sourceParameter =>
                    !existingParameters.TryGetValue(sourceParameter.ParameterCode.Trim(), out var existingParameter) ||
                    existingParameter.SequenceNo != sourceParameter.SequenceNo);
            }
        }

        var existingTransitionsById = entity.StageTransitions.ToDictionary(
            transition => transition.ManufacturingProcessTemplateStageTransitionId);
        var existingTransitionsByCode = entity.StageTransitions.ToDictionary(
            transition => transition.Code,
            StringComparer.OrdinalIgnoreCase);
        var transitionChanges = request.StageTransitions.Any(source =>
        {
            ManufacturingProcessTemplateStageTransition? existing = null;
            if (source.ManufacturingProcessTemplateStageTransitionId.HasValue)
                existingTransitionsById.TryGetValue(source.ManufacturingProcessTemplateStageTransitionId.Value, out existing);
            else
                existingTransitionsByCode.TryGetValue(source.Code.Trim(), out existing);
            return existing is null || existing.SequenceNo != source.SequenceNo;
        });

        return new SequenceStagingPlan(stageChanges, machineChanges, parameterChanges, transitionChanges);
    }

    private readonly record struct SequenceStagingPlan(
        bool Stages,
        bool Machines,
        bool Parameters,
        bool Transitions)
    {
        internal bool HasChanges => Stages || Machines || Parameters || Transitions;
    }

    private static string? ValidateApplicabilityRules(
        IReadOnlyList<ManufacturingProcessTemplateApplicabilityWriteDto> rules)
    {
        if (rules.Any(rule =>
                (!rule.CategoryId.HasValue && !rule.StepOfProduct.HasValue) ||
                rule.Priority < 0 ||
                rule.StepOfProduct.HasValue && !Enum.IsDefined(rule.StepOfProduct.Value)))
        {
            return "Each applicability rule must contain CategoryId or StepOfProduct, use a valid StepOfProduct and have a non-negative Priority.";
        }

        if (rules
            .GroupBy(rule => new { rule.CategoryId, rule.StepOfProduct })
            .Any(group => group.Count() > 1))
        {
            return "CategoryId and StepOfProduct must be unique within a process template applicability list.";
        }

        return null;
    }

    private static string? ValidateMachineParameters(
        IReadOnlyList<ManufacturingProcessTemplateStageWriteDto> stages)
    {
        foreach (var machine in stages.SelectMany(stage => stage.Machines))
        {
            if (machine.Parameters.Any(parameter =>
                    string.IsNullOrWhiteSpace(parameter.ParameterCode) || parameter.ParameterCode.Trim().Length > 64 ||
                    string.IsNullOrWhiteSpace(parameter.ParameterName) || parameter.ParameterName.Trim().Length > 200 ||
                    string.IsNullOrWhiteSpace(parameter.Unit) || parameter.Unit.Trim().Length > 32 ||
                    parameter.SequenceNo <= 0))
                return "Machine parameter Code, Name, Unit and SequenceNo must be valid.";

            var hasDuplicateCode = machine.Parameters
                .GroupBy(
                    parameter => parameter.ParameterCode.Trim(),
                    StringComparer.OrdinalIgnoreCase)
                .Any(group => group.Count() > 1);
            var hasDuplicateSequence = machine.Parameters
                .GroupBy(parameter => parameter.SequenceNo)
                .Any(group => group.Count() > 1);
            if (hasDuplicateCode || hasDuplicateSequence)
            {
                return "Machine parameter Code and SequenceNo must be unique within a machine.";
            }

            if (machine.Parameters.Any(parameter =>
                    (parameter.MinValue.HasValue && parameter.MaxValue.HasValue && parameter.MinValue > parameter.MaxValue) ||
                    (parameter.TargetValue.HasValue && parameter.MinValue.HasValue && parameter.TargetValue < parameter.MinValue) ||
                    (parameter.TargetValue.HasValue && parameter.MaxValue.HasValue && parameter.TargetValue > parameter.MaxValue)))
                return "Machine parameter MinValue, TargetValue and MaxValue must form a valid range when provided.";
        }

        return null;
    }

    private static string? ValidateMachineConfigurationGroups(
        IReadOnlyList<ManufacturingProcessTemplateStageWriteDto> stages)
    {
        foreach (var stage in stages)
        {
            if (stage.Machines.Any(machine =>
                    machine.ConfigurationGroupKey == Guid.Empty ||
                    machine.ConfigurationGroupName?.Trim().Length > 200 ||
                    machine.ConfigurationGroupKey.HasValue && string.IsNullOrWhiteSpace(machine.ConfigurationGroupName) ||
                    !machine.ConfigurationGroupKey.HasValue && !string.IsNullOrWhiteSpace(machine.ConfigurationGroupName)))
                return $"Stage {stage.Code} has an invalid machine configuration group name or a group name without ConfigurationGroupKey.";

            var configurationGroups = stage.Machines
                .Where(machine => machine.ConfigurationGroupKey.HasValue)
                .GroupBy(machine => machine.ConfigurationGroupKey!.Value);
            foreach (var group in configurationGroups)
            {
                var reference = group.First();
                if (group.Skip(1).Any(machine =>
                        !ManufacturingProcessTemplateMachineConfigurationRules.HasSameSharedConfiguration(reference, machine)))
                    return $"Machines in configuration group {group.Key} of stage {stage.Code} must have the same group name, note and parameters.";
            }
        }

        return null;
    }

    private static string? ValidateStageTransitions(UpsertManufacturingProcessTemplateRequest request)
    {
        var stageCodes = request.Stages
            .Select(stage => stage.Code.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (request.StageTransitions.Any(transition =>
                string.IsNullOrWhiteSpace(transition.Code) ||
                transition.Code.Trim().Length > 64 ||
                string.IsNullOrWhiteSpace(transition.FromStageCode) ||
                string.IsNullOrWhiteSpace(transition.ToStageCode) ||
                transition.SequenceNo <= 0 ||
                string.Equals(
                    transition.FromStageCode.Trim(),
                    transition.ToStageCode.Trim(),
                    StringComparison.OrdinalIgnoreCase) ||
                !stageCodes.Contains(transition.FromStageCode.Trim()) ||
                !stageCodes.Contains(transition.ToStageCode.Trim()) ||
                !Enum.IsDefined(transition.TransitionType) ||
                transition.DefaultEventCount <= 0))
        {
            return "Each stage transition must have a valid Code and reference two different submitted stages with valid Type, SequenceNo and DefaultEventCount.";
        }

        var hasDuplicateCode = request.StageTransitions
            .GroupBy(transition => transition.Code.Trim(), StringComparer.OrdinalIgnoreCase)
            .Any(group => group.Count() > 1);
        var hasDuplicateSequence = request.StageTransitions
            .GroupBy(transition => transition.SequenceNo)
            .Any(group => group.Count() > 1);
        var hasDuplicateStagePair = request.StageTransitions
            .GroupBy(transition =>
                $"{transition.FromStageCode.Trim().ToUpperInvariant()}->" +
                transition.ToStageCode.Trim().ToUpperInvariant())
            .Any(group => group.Count() > 1);
        if (hasDuplicateCode || hasDuplicateSequence || hasDuplicateStagePair)
        {
            return "Stage transition Code, SequenceNo and From/To stage pair must be unique.";
        }

        return null;
    }
}
