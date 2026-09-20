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

internal sealed class UpsertManufacturingWorkInstructionCommandHandler : IRequestHandler<UpsertManufacturingWorkInstructionCommand, OperationResult<ManufacturingWorkInstructionTemplateDto>>
{
    private readonly IPLMWriteDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IExternalIdService _externalIdService;

    public UpsertManufacturingWorkInstructionCommandHandler(IPLMWriteDbContext db, ICurrentUser currentUser, IExternalIdService externalIdService)
    {
        _db = db;
        _currentUser = currentUser;
        _externalIdService = externalIdService;
    }

    public async Task<OperationResult<ManufacturingWorkInstructionTemplateDto>> Handle(UpsertManufacturingWorkInstructionCommand command, CancellationToken cancellationToken)
    {
        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty || _currentUser.EmployeeId is not { } employeeId || employeeId == Guid.Empty)
            return OperationResult<ManufacturingWorkInstructionTemplateDto>.Fail("Current company or employee is invalid.");
        var request = command.Request;
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Procedure))
            return OperationResult<ManufacturingWorkInstructionTemplateDto>.Fail("Name and Procedure are required.");
        if (request.EffectiveTo < request.EffectiveFrom)
            return OperationResult<ManufacturingWorkInstructionTemplateDto>.Fail("EffectiveTo must not be earlier than EffectiveFrom.");
        if (request.ChecklistItems.Any(x => string.IsNullOrWhiteSpace(x.Content) || x.SequenceNo <= 0) || request.ChecklistItems.GroupBy(x => x.SequenceNo).Any(x => x.Count() > 1))
            return OperationResult<ManufacturingWorkInstructionTemplateDto>.Fail("Checklist Content and SequenceNo must be valid and SequenceNo must be unique.");
        var submittedIds = request.ChecklistItems.Where(x => x.ManufacturingWorkInstructionChecklistItemId.HasValue).Select(x => x.ManufacturingWorkInstructionChecklistItemId!.Value).ToList();
        if (submittedIds.Count != submittedIds.Distinct().Count())
            return OperationResult<ManufacturingWorkInstructionTemplateDto>.Fail("Checklist item IDs must be unique.");

        ManufacturingWorkInstructionTemplate entity;
        var isCreate = !command.TemplateId.HasValue;
        if (command.TemplateId is { } id)
        {
            entity = await _db.ManufacturingWorkInstructionTemplates.Include(x => x.ChecklistItems).FirstOrDefaultAsync(x => x.ManufacturingWorkInstructionTemplateId == id && x.CompanyId == companyId, cancellationToken) ?? null!;
            if (entity is null) return OperationResult<ManufacturingWorkInstructionTemplateDto>.Fail("Work Instruction was not found.");
            if (entity.Status != ManufacturingTemplateStatus.Draft) return OperationResult<ManufacturingWorkInstructionTemplateDto>.Fail("Only Draft Work Instructions can be changed.");
            if (submittedIds.Except(entity.ChecklistItems.Select(x => x.ManufacturingWorkInstructionChecklistItemId)).Any())
                return OperationResult<ManufacturingWorkInstructionTemplateDto>.Fail("A checklist item does not belong to this Work Instruction.");
            _db.ManufacturingWorkInstructionChecklistItems.RemoveRange(entity.ChecklistItems.Where(x => !submittedIds.Contains(x.ManufacturingWorkInstructionChecklistItemId)));
            entity.UpdatedDate = DateTime.Now;
            entity.UpdatedBy = employeeId;
        }
        else
        {
            if (submittedIds.Count > 0) return OperationResult<ManufacturingWorkInstructionTemplateDto>.Fail("New Work Instructions cannot reference existing checklist item IDs.");
            entity = new ManufacturingWorkInstructionTemplate
            {
                ManufacturingWorkInstructionTemplateId = Guid.CreateVersion7(), CompanyId = companyId,
                ExternalId = await _externalIdService.GenerateGlobalCodeAsync(companyId, DocumentPrefix.WI.ToString(), cancellationToken),
                VersionNo = 1, CreatedDate = DateTime.Now, CreatedBy = employeeId
            };
            await _db.ManufacturingWorkInstructionTemplates.AddAsync(entity, cancellationToken);
        }

        entity.Name = request.Name.Trim(); entity.Purpose = Normalize(request.Purpose); entity.Preparation = Normalize(request.Preparation);
        entity.Procedure = request.Procedure.Trim(); entity.QualityRequirements = Normalize(request.QualityRequirements); entity.SafetyNotes = Normalize(request.SafetyNotes);
        entity.EffectiveFrom = request.EffectiveFrom; entity.EffectiveTo = request.EffectiveTo;

        var existingById = entity.ChecklistItems.ToDictionary(x => x.ManufacturingWorkInstructionChecklistItemId);
        var updatedItems = new List<ManufacturingWorkInstructionChecklistItem>();
        foreach (var item in request.ChecklistItems)
        {
            ManufacturingWorkInstructionChecklistItem target;
            if (item.ManufacturingWorkInstructionChecklistItemId is { } itemId) target = existingById[itemId];
            else target = new ManufacturingWorkInstructionChecklistItem
            {
                ManufacturingWorkInstructionChecklistItemId = Guid.CreateVersion7(), ManufacturingWorkInstructionTemplateId = entity.ManufacturingWorkInstructionTemplateId,
                ExternalId = await _externalIdService.GenerateGlobalCodeAsync(companyId, DocumentPrefix.WIC.ToString(), cancellationToken)
            };
            target.Content = item.Content.Trim(); target.SequenceNo = item.SequenceNo; target.IsRequired = item.IsRequired;
            target.ExpectedValue = Normalize(item.ExpectedValue); target.Unit = Normalize(item.Unit); updatedItems.Add(target);
        }
        entity.ChecklistItems = updatedItems;
        _db.AuditLogs.Add(BomAudit.Create(companyId, employeeId, "manufacturing_work_instruction_templates", entity.ManufacturingWorkInstructionTemplateId,
            isCreate ? "CreateWorkInstructionTemplate" : "UpdateWorkInstructionTemplate", new { entity.ExternalId, entity.VersionNo, ChecklistCount = entity.ChecklistItems.Count }, actionType: isCreate ? AuditActionType.Create : AuditActionType.Update));
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<ManufacturingWorkInstructionTemplateDto>.Ok(ManufacturingTemplateMapper.ToDto(entity));
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
