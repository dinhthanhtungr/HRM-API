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

namespace HRM.Application.Features.PLM.Boms.Commands.ChangeManufacturingProcessTemplateStatus;

internal sealed class ChangeManufacturingProcessTemplateStatusCommandHandler : IRequestHandler<ChangeManufacturingProcessTemplateStatusCommand, OperationResult<ManufacturingProcessTemplateDto>>
{
    private readonly IPLMWriteDbContext _db; private readonly ICurrentUser _currentUser;
    public ChangeManufacturingProcessTemplateStatusCommandHandler(IPLMWriteDbContext db, ICurrentUser currentUser) { _db = db; _currentUser = currentUser; }
    public async Task<OperationResult<ManufacturingProcessTemplateDto>> Handle(ChangeManufacturingProcessTemplateStatusCommand command, CancellationToken cancellationToken)
    {
        if (_currentUser.CompanyId is not { } companyId || _currentUser.EmployeeId is not { } employeeId) return OperationResult<ManufacturingProcessTemplateDto>.Fail("Current company or employee is invalid.");
        var entity = await _db.ManufacturingProcessTemplates
            .Include(x => x.Stages).ThenInclude(x => x.WorkInstructionTemplate)
            .Include(x => x.Stages).ThenInclude(x => x.Machines).ThenInclude(x => x.Equipment)
            .FirstOrDefaultAsync(x => x.ManufacturingProcessTemplateId == command.TemplateId && x.CompanyId == companyId, cancellationToken);
        if (entity is null) return OperationResult<ManufacturingProcessTemplateDto>.Fail("Process template was not found.");
        if (command.Action == ManufacturingTemplateLifecycleAction.Release)
        {
            if (entity.Status != ManufacturingTemplateStatus.Draft || entity.Stages.Count == 0) return OperationResult<ManufacturingProcessTemplateDto>.Fail("Only a Draft process template with stages can be released.");
            if (entity.Stages.Any(x => x.WorkInstructionTemplate != null && x.WorkInstructionTemplate.Status != ManufacturingTemplateStatus.Released)) return OperationResult<ManufacturingProcessTemplateDto>.Fail("Every assigned Work Instruction must still be Released.");
            entity.Status = ManufacturingTemplateStatus.Released; entity.ReleasedDate = DateTime.Now; entity.ReleasedBy = employeeId;
        }
        else
        {
            if (entity.Status != ManufacturingTemplateStatus.Released) return OperationResult<ManufacturingProcessTemplateDto>.Fail("Only Released process templates can be made obsolete.");
            entity.Status = ManufacturingTemplateStatus.Obsolete;
        }
        entity.UpdatedDate = DateTime.Now; entity.UpdatedBy = employeeId;
        _db.AuditLogs.Add(BomAudit.Create(companyId, employeeId, "manufacturing_process_templates", entity.ManufacturingProcessTemplateId,
            command.Action == ManufacturingTemplateLifecycleAction.Release ? "ReleaseManufacturingProcessTemplate" : "ObsoleteManufacturingProcessTemplate",
            new { entity.ExternalId, entity.VersionNo, entity.Status }, actionType: AuditActionType.Update));
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<ManufacturingProcessTemplateDto>.Ok(ManufacturingTemplateMapper.ToDto(entity));
    }
}
