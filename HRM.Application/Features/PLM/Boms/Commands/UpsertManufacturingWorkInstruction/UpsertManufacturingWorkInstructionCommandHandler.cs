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

namespace HRM.Application.Features.PLM.Boms.Commands.UpsertManufacturingWorkInstruction;

internal sealed class UpsertManufacturingWorkInstructionCommandHandler
    : IRequestHandler<
        UpsertManufacturingWorkInstructionCommand,
        OperationResult<ManufacturingWorkInstructionTemplateDto>>
{
    private readonly IPLMWriteDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IExternalIdService _externalIdService;

    public UpsertManufacturingWorkInstructionCommandHandler(
        IPLMWriteDbContext db,
        ICurrentUser currentUser,
        IExternalIdService externalIdService)
    {
        _db = db;
        _currentUser = currentUser;
        _externalIdService = externalIdService;
    }

    public async Task<OperationResult<ManufacturingWorkInstructionTemplateDto>> Handle(
        UpsertManufacturingWorkInstructionCommand command,
        CancellationToken cancellationToken)
    {
        // Resolve tenant and actor before reading or changing the aggregate.
        if (_currentUser.CompanyId is not { } companyId ||
            companyId == Guid.Empty ||
            _currentUser.EmployeeId is not { } employeeId ||
            employeeId == Guid.Empty)
        {
            return OperationResult<ManufacturingWorkInstructionTemplateDto>.Fail(
                "Current company or employee is invalid.");
        }

        // Validate the complete replacement payload.
        var request = command.Request;
        var savedAt = DateTime.Now;
        var effectiveFrom = request.EffectiveFrom ?? savedAt;
        if (request.EffectiveTo.HasValue && request.EffectiveTo.Value < effectiveFrom)
        {
            return OperationResult<ManufacturingWorkInstructionTemplateDto>.Fail(
                "EffectiveTo must not be earlier than EffectiveFrom.");
        }

        var hasInvalidChecklistItem = request.ChecklistItems.Any(item =>
            string.IsNullOrWhiteSpace(item.Content) || item.SequenceNo <= 0);
        var hasDuplicateChecklistSequence = request.ChecklistItems
            .GroupBy(item => item.SequenceNo)
            .Any(group => group.Count() > 1);
        if (hasInvalidChecklistItem || hasDuplicateChecklistSequence)
        {
            return OperationResult<ManufacturingWorkInstructionTemplateDto>.Fail(
                "Checklist Content and SequenceNo must be valid and SequenceNo must be unique.");
        }

        var submittedIds = request.ChecklistItems
            .Where(item => item.ManufacturingWorkInstructionChecklistItemId.HasValue)
            .Select(item => item.ManufacturingWorkInstructionChecklistItemId!.Value)
            .ToList();
        if (submittedIds.Count != submittedIds.Distinct().Count())
        {
            return OperationResult<ManufacturingWorkInstructionTemplateDto>.Fail("Checklist item IDs must be unique.");
        }

        // Load the existing Draft or initialize a new Work Instruction.
        ManufacturingWorkInstructionTemplate entity;
        var isCreate = !command.TemplateId.HasValue;
        if (command.TemplateId is { } id)
        {
            entity = await _db.ManufacturingWorkInstructionTemplates
                .Include(instruction => instruction.ChecklistItems)
                .FirstOrDefaultAsync(
                    instruction => instruction.ManufacturingWorkInstructionTemplateId == id &&
                                   instruction.CompanyId == companyId,
                    cancellationToken) ?? null!;
            if (entity is null)
            {
                return OperationResult<ManufacturingWorkInstructionTemplateDto>.Fail(
                    "Work Instruction was not found.");
            }

            if (entity.Status != ManufacturingTemplateStatus.Draft)
            {
                return OperationResult<ManufacturingWorkInstructionTemplateDto>.Fail(
                    "Only Draft Work Instructions can be changed.");
            }

            if (submittedIds.Except(
                    entity.ChecklistItems.Select(
                        item => item.ManufacturingWorkInstructionChecklistItemId)).Any())
            {
                return OperationResult<ManufacturingWorkInstructionTemplateDto>.Fail(
                    "A checklist item does not belong to this Work Instruction.");
            }

            _db.ManufacturingWorkInstructionChecklistItems.RemoveRange(
                entity.ChecklistItems.Where(
                    item => !submittedIds.Contains(
                        item.ManufacturingWorkInstructionChecklistItemId)));
            entity.UpdatedDate = savedAt;
            entity.UpdatedBy = employeeId;
        }
        else
        {
            if (submittedIds.Count > 0)
            {
                return OperationResult<ManufacturingWorkInstructionTemplateDto>.Fail(
                    "New Work Instructions cannot reference existing checklist item IDs.");
            }

            entity = new ManufacturingWorkInstructionTemplate
            {
                ManufacturingWorkInstructionTemplateId = Guid.CreateVersion7(),
                CompanyId = companyId,
                ExternalId = await _externalIdService.GenerateGlobalCodeAsync(
                    companyId,
                    DocumentPrefix.WI.ToString(),
                    cancellationToken),
                VersionNo = 1,
                CreatedDate = savedAt,
                CreatedBy = employeeId
            };
            await _db.ManufacturingWorkInstructionTemplates.AddAsync(entity, cancellationToken);
        }

        // Apply the submitted Work Instruction and checklist.
        entity.Name = Normalize(request.Name) ?? string.Empty;
        entity.Purpose = Normalize(request.Purpose);
        entity.Preparation = Normalize(request.Preparation);
        entity.Procedure = Normalize(request.Procedure) ?? string.Empty;
        entity.QualityRequirements = Normalize(request.QualityRequirements);
        entity.SafetyNotes = Normalize(request.SafetyNotes);
        entity.EffectiveFrom = effectiveFrom;
        entity.EffectiveTo = request.EffectiveTo;

        var existingById = entity.ChecklistItems.ToDictionary(
            item => item.ManufacturingWorkInstructionChecklistItemId);
        var updatedItems = new List<ManufacturingWorkInstructionChecklistItem>();
        foreach (var item in request.ChecklistItems)
        {
            ManufacturingWorkInstructionChecklistItem target;
            if (item.ManufacturingWorkInstructionChecklistItemId is { } itemId)
            {
                target = existingById[itemId];
            }
            else
            {
                target = new ManufacturingWorkInstructionChecklistItem
                {
                    ManufacturingWorkInstructionChecklistItemId = Guid.CreateVersion7(),
                    ManufacturingWorkInstructionTemplateId =
                        entity.ManufacturingWorkInstructionTemplateId,
                    ExternalId = await _externalIdService.GenerateGlobalCodeAsync(
                        companyId,
                        DocumentPrefix.WIC.ToString(),
                        cancellationToken)
                };
                _db.ManufacturingWorkInstructionChecklistItems.Add(target);
            }

            target.Content = item.Content.Trim();
            target.SequenceNo = item.SequenceNo;
            target.IsRequired = item.IsRequired;
            target.ExpectedValue = Normalize(item.ExpectedValue);
            target.Unit = Normalize(item.Unit);
            updatedItems.Add(target);
        }

        entity.ChecklistItems = updatedItems;
        _db.AuditLogs.Add(BomAudit.Create(
            companyId,
            employeeId,
            "manufacturing_work_instruction_templates",
            entity.ManufacturingWorkInstructionTemplateId,
            isCreate ? "CreateWorkInstructionTemplate" : "UpdateWorkInstructionTemplate",
            new
            {
                entity.ExternalId,
                entity.VersionNo,
                ChecklistCount = entity.ChecklistItems.Count
            },
            actionType: isCreate ? AuditActionType.Create : AuditActionType.Update));

        // Persist the Work Instruction before returning its generated identifiers.
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<ManufacturingWorkInstructionTemplateDto>.Ok(
            ManufacturingTemplateMapper.ToDto(entity));
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
