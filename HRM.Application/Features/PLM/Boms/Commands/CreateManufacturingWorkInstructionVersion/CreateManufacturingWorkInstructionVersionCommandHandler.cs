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

namespace HRM.Application.Features.PLM.Boms.Commands.CreateManufacturingWorkInstructionVersion;

internal sealed class CreateManufacturingWorkInstructionVersionCommandHandler
    : IRequestHandler<
        CreateManufacturingWorkInstructionVersionCommand,
        OperationResult<ManufacturingWorkInstructionTemplateDto>>
{
    private readonly IPLMWriteDbContext _db;
    private readonly ICurrentUser _currentUser;

    public CreateManufacturingWorkInstructionVersionCommandHandler(
        IPLMWriteDbContext db,
        ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<OperationResult<ManufacturingWorkInstructionTemplateDto>> Handle(
        CreateManufacturingWorkInstructionVersionCommand command,
        CancellationToken cancellationToken)
    {
        if (_currentUser.CompanyId is not { } companyId ||
            _currentUser.EmployeeId is not { } employeeId)
        {
            return OperationResult<ManufacturingWorkInstructionTemplateDto>.Fail(
                "Current company or employee is invalid.");
        }

        var source = await _db.ManufacturingWorkInstructionTemplates
            .AsNoTracking()
            .Include(instruction => instruction.ChecklistItems)
            .FirstOrDefaultAsync(
                instruction =>
                    instruction.ManufacturingWorkInstructionTemplateId == command.SourceTemplateId &&
                    instruction.CompanyId == companyId &&
                    instruction.Status != ManufacturingTemplateStatus.Draft,
                cancellationToken);
        if (source is null)
        {
            return OperationResult<ManufacturingWorkInstructionTemplateDto>.Fail(
                "A Released or Obsolete source Work Instruction was not found.");
        }

        var versionNo = await _db.ManufacturingWorkInstructionTemplates
            .Where(instruction =>
                instruction.CompanyId == companyId &&
                instruction.ExternalId == source.ExternalId)
            .MaxAsync(instruction => instruction.VersionNo, cancellationToken) + 1;

        var entity = new ManufacturingWorkInstructionTemplate
        {
            ManufacturingWorkInstructionTemplateId = Guid.CreateVersion7(),
            CompanyId = companyId,
            ExternalId = source.ExternalId,
            Name = source.Name,
            VersionNo = versionNo,
            Purpose = source.Purpose,
            Preparation = source.Preparation,
            Procedure = source.Procedure,
            QualityRequirements = source.QualityRequirements,
            SafetyNotes = source.SafetyNotes,
            EffectiveFrom = source.EffectiveFrom,
            EffectiveTo = source.EffectiveTo,
            CreatedDate = DateTime.Now,
            CreatedBy = employeeId,
            ChecklistItems = source.ChecklistItems
                .Where(item => item.IsActive)
                .Select(item => new ManufacturingWorkInstructionChecklistItem
                {
                    ManufacturingWorkInstructionChecklistItemId = Guid.CreateVersion7(),
                    ExternalId = item.ExternalId,
                    Content = item.Content,
                    SequenceNo = item.SequenceNo,
                    IsRequired = item.IsRequired,
                    ExpectedValue = item.ExpectedValue,
                    Unit = item.Unit
                })
                .ToList()
        };

        await _db.ManufacturingWorkInstructionTemplates.AddAsync(entity, cancellationToken);
        _db.AuditLogs.Add(BomAudit.Create(
            companyId,
            employeeId,
            "manufacturing_work_instruction_templates",
            entity.ManufacturingWorkInstructionTemplateId,
            "CreateWorkInstructionTemplateVersion",
            new
            {
                SourceTemplateId = source.ManufacturingWorkInstructionTemplateId,
                entity.ExternalId,
                entity.VersionNo,
                command.ChangeReason
            },
            actionType: AuditActionType.Create));
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<ManufacturingWorkInstructionTemplateDto>.Ok(
            ManufacturingTemplateMapper.ToDto(entity));
    }
}
