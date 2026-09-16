using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Application.Features.PLM.Boms.Mappers;
using HRM.Application.Features.PLM.Boms.Rules;
using HRM.Application.Features.PLM.Boms.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Boms.Commands.PatchManufacturingLossType;

internal sealed class PatchManufacturingLossTypeCommandHandler
    : IRequestHandler<PatchManufacturingLossTypeCommand, OperationResult<ManufacturingLossTypeDto>>
{
    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public PatchManufacturingLossTypeCommandHandler(IPLMWriteDbContext dbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<OperationResult<ManufacturingLossTypeDto>> Handle(
        PatchManufacturingLossTypeCommand command,
        CancellationToken cancellationToken)
    {
        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty ||
            _currentUser.EmployeeId is not { } employeeId || employeeId == Guid.Empty)
        {
            return OperationResult<ManufacturingLossTypeDto>.Fail("Current company or employee is invalid.");
        }

        var entity = await _dbContext.ManufacturingLossTypes.FirstOrDefaultAsync(
            x => x.ManufacturingLossTypeId == command.ManufacturingLossTypeId && x.CompanyId == companyId,
            cancellationToken);
        if (entity is null)
        {
            return OperationResult<ManufacturingLossTypeDto>.Fail("Loss type was not found.");
        }

        var request = command.Request;
        if (request.Name is not null)
        {
            var error = BomRules.ValidateText(request.Name, 200, nameof(request.Name), true);
            if (error is not null) return OperationResult<ManufacturingLossTypeDto>.Fail(error);
            entity.Name = request.Name.Trim();
        }
        if (request.Description is not null) entity.Description = BomRules.NormalizeOptionalText(request.Description);
        if (request.ClearDescription) entity.Description = null;
        if (request.DefaultCalculationMethod.HasValue)
        {
            if (!Enum.IsDefined(request.DefaultCalculationMethod.Value))
                return OperationResult<ManufacturingLossTypeDto>.Fail("DefaultCalculationMethod is invalid.");
            entity.DefaultCalculationMethod = request.DefaultCalculationMethod.Value;
        }
        if (request.IsRecoverable.HasValue) entity.IsRecoverable = request.IsRecoverable.Value;
        if (request.IsActive.HasValue) entity.IsActive = request.IsActive.Value;
        entity.UpdatedDate = DateTime.Now;
        entity.UpdatedBy = employeeId;

        _dbContext.AuditLogs.Add(BomAudit.Create(
            companyId,
            employeeId,
            "manufacturing_loss_types",
            entity.ManufacturingLossTypeId,
            "PatchManufacturingLossType",
            new { entity.Name, entity.DefaultCalculationMethod, entity.IsRecoverable, entity.IsActive }));

        await _dbContext.SaveChangesAsync(cancellationToken);
        return OperationResult<ManufacturingLossTypeDto>.Ok(ManufacturingLossTypeMapper.ToDto(entity));
    }
}
