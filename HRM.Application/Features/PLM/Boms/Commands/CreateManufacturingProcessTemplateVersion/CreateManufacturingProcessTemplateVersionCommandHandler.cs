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

internal sealed class CreateManufacturingProcessTemplateVersionCommandHandler : IRequestHandler<CreateManufacturingProcessTemplateVersionCommand, OperationResult<ManufacturingProcessTemplateDto>>
{
    private readonly IPLMWriteDbContext _db; private readonly ICurrentUser _currentUser;
    public CreateManufacturingProcessTemplateVersionCommandHandler(IPLMWriteDbContext db, ICurrentUser currentUser) { _db = db; _currentUser = currentUser; }
    public async Task<OperationResult<ManufacturingProcessTemplateDto>> Handle(CreateManufacturingProcessTemplateVersionCommand command, CancellationToken cancellationToken)
    {
        if (_currentUser.CompanyId is not { } companyId || _currentUser.EmployeeId is not { } employeeId) return OperationResult<ManufacturingProcessTemplateDto>.Fail("Current company or employee is invalid.");
        var source = await _db.ManufacturingProcessTemplates.AsNoTracking().Include(x => x.Stages).ThenInclude(x => x.WorkInstructionTemplate).Include(x => x.Stages).ThenInclude(x => x.Machines).ThenInclude(x => x.Equipment).FirstOrDefaultAsync(x => x.ManufacturingProcessTemplateId == command.SourceTemplateId && x.CompanyId == companyId && x.Status != ManufacturingTemplateStatus.Draft, cancellationToken);
        if (source is null) return OperationResult<ManufacturingProcessTemplateDto>.Fail("A Released or Obsolete source process template was not found.");
        var versionNo = await _db.ManufacturingProcessTemplates.Where(x => x.CompanyId == companyId && x.ExternalId == source.ExternalId).MaxAsync(x => x.VersionNo, cancellationToken) + 1;
        var entity = new ManufacturingProcessTemplate
        {
            ManufacturingProcessTemplateId = Guid.CreateVersion7(), CompanyId = companyId, ExternalId = source.ExternalId, Name = source.Name, Description = source.Description, VersionNo = versionNo,
            EffectiveFrom = source.EffectiveFrom, EffectiveTo = source.EffectiveTo, CreatedDate = DateTime.Now, CreatedBy = employeeId,
            Stages = source.Stages.Where(x => x.IsActive).Select(x => new ManufacturingProcessTemplateStage
            {
                ManufacturingProcessTemplateStageId = Guid.CreateVersion7(), ExternalId = x.ExternalId, Name = x.Name, Description = x.Description, SequenceNo = x.SequenceNo,
                ManufacturingWorkInstructionTemplateId = x.ManufacturingWorkInstructionTemplateId,
                Machines = x.Machines.Select(m => new ManufacturingProcessTemplateStageMachine { ManufacturingProcessTemplateStageMachineId = Guid.CreateVersion7(), EquipmentId = m.EquipmentId, IsDefault = m.IsDefault, SequenceNo = m.SequenceNo, Note = m.Note }).ToList()
            }).ToList()
        };
        await _db.ManufacturingProcessTemplates.AddAsync(entity, cancellationToken);
        _db.AuditLogs.Add(BomAudit.Create(companyId, employeeId, "manufacturing_process_templates", entity.ManufacturingProcessTemplateId, "CreateManufacturingProcessTemplateVersion", new { SourceTemplateId = source.ManufacturingProcessTemplateId, entity.ExternalId, entity.VersionNo, command.ChangeReason }, actionType: AuditActionType.Create));
        await _db.SaveChangesAsync(cancellationToken);
        var response = await _db.ManufacturingProcessTemplates.AsNoTracking().Include(x => x.Stages).ThenInclude(x => x.WorkInstructionTemplate).Include(x => x.Stages).ThenInclude(x => x.Machines).ThenInclude(x => x.Equipment).FirstAsync(x => x.ManufacturingProcessTemplateId == entity.ManufacturingProcessTemplateId, cancellationToken);
        return OperationResult<ManufacturingProcessTemplateDto>.Ok(ManufacturingTemplateMapper.ToDto(response));
    }
}
