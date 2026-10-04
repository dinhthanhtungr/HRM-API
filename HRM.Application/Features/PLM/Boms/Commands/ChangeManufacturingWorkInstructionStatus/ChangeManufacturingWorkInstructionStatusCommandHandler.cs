using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Application.Features.PLM.Boms.Mappers;
using HRM.Application.Features.PLM.Boms.Services;
using HRM.Domain.Enums.Audits;
using HRM.Domain.Enums.Boms;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Boms.Commands.ChangeManufacturingWorkInstructionStatus;

internal sealed class ChangeManufacturingWorkInstructionStatusCommandHandler
    : IRequestHandler<
        ChangeManufacturingWorkInstructionStatusCommand,
        OperationResult<ManufacturingWorkInstructionTemplateDto>>
{
    private readonly IPLMWriteDbContext _db;
    private readonly ICurrentUser _currentUser;

    public ChangeManufacturingWorkInstructionStatusCommandHandler(
        IPLMWriteDbContext db,
        ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<OperationResult<ManufacturingWorkInstructionTemplateDto>> Handle(
        ChangeManufacturingWorkInstructionStatusCommand command,
        CancellationToken cancellationToken)
    {
        if (_currentUser.CompanyId is not { } companyId ||
            _currentUser.EmployeeId is not { } employeeId)
        {
            return OperationResult<ManufacturingWorkInstructionTemplateDto>.Fail(
                "Current company or employee is invalid.");
        }

        var entity = await _db.ManufacturingWorkInstructionTemplates
            .Include(instruction => instruction.ChecklistItems)
            .FirstOrDefaultAsync(
                instruction => instruction.ManufacturingWorkInstructionTemplateId == command.TemplateId &&
                               instruction.CompanyId == companyId,
                cancellationToken);
        if (entity is null)
        {
            return OperationResult<ManufacturingWorkInstructionTemplateDto>.Fail(
                "Work Instruction was not found.");
        }

        if (command.Action == ManufacturingTemplateLifecycleAction.Release)
        {
            if (entity.Status != ManufacturingTemplateStatus.Draft)
            {
                return OperationResult<ManufacturingWorkInstructionTemplateDto>.Fail(
                    "Only Draft Work Instructions can be released.");
            }

            entity.Status = ManufacturingTemplateStatus.Released;
            entity.ReleasedDate = DateTime.Now;
            entity.ReleasedBy = employeeId;
        }
        else
        {
            if (entity.Status != ManufacturingTemplateStatus.Released)
            {
                return OperationResult<ManufacturingWorkInstructionTemplateDto>.Fail(
                    "Only Released Work Instructions can be made obsolete.");
            }

            entity.Status = ManufacturingTemplateStatus.Obsolete;
        }

        entity.UpdatedDate = DateTime.Now;
        entity.UpdatedBy = employeeId;
        _db.AuditLogs.Add(BomAudit.Create(
            companyId,
            employeeId,
            "manufacturing_work_instruction_templates",
            entity.ManufacturingWorkInstructionTemplateId,
            command.Action == ManufacturingTemplateLifecycleAction.Release
                ? "ReleaseWorkInstructionTemplate"
                : "ObsoleteWorkInstructionTemplate",
            new { entity.ExternalId, entity.VersionNo, entity.Status },
            actionType: AuditActionType.Update));
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<ManufacturingWorkInstructionTemplateDto>.Ok(
            ManufacturingTemplateMapper.ToDto(entity));
    }
}
