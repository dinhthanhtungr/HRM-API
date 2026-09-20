using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Application.Features.PLM.Boms.Mappers;
using HRM.Application.Features.PLM.Boms.Rules;
using HRM.Application.Features.PLM.Boms.Services;
using HRM.Domain.Entities.BomSchema;
using HRM.Domain.Enums.Audits;
using HRM.Domain.Enums.Boms;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Boms.Commands.CreateManufacturingLossType;

internal sealed class CreateManufacturingLossTypeCommandHandler
    : IRequestHandler<CreateManufacturingLossTypeCommand, OperationResult<ManufacturingLossTypeDto>>
{
    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public CreateManufacturingLossTypeCommandHandler(IPLMWriteDbContext dbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<OperationResult<ManufacturingLossTypeDto>> Handle(
        CreateManufacturingLossTypeCommand command,
        CancellationToken cancellationToken)
    {
        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty ||
            _currentUser.EmployeeId is not { } employeeId || employeeId == Guid.Empty)
        {
            return OperationResult<ManufacturingLossTypeDto>.Fail("Current company or employee is invalid.");
        }

        var request = command.Request;
        var error = BomRules.ValidateText(request.Code, 64, nameof(request.Code), true)
            ?? BomRules.ValidateText(request.Name, 200, nameof(request.Name), true);
        if (error is not null || !Enum.IsDefined(request.DefaultCalculationMethod))
        {
            return OperationResult<ManufacturingLossTypeDto>.Fail(error ?? "DefaultCalculationMethod is invalid.");
        }

        var code = request.Code.Trim();
        if (await _dbContext.ManufacturingLossTypes.AsNoTracking()
            .AnyAsync(x => x.CompanyId == companyId && x.ExternalId == code, cancellationToken))
        {
            return OperationResult<ManufacturingLossTypeDto>.Fail("Loss type code already exists in your company.");
        }

        var entity = new ManufacturingLossType
        {
            ManufacturingLossTypeId = Guid.CreateVersion7(),
            CompanyId = companyId,
            ExternalId = code,
            Name = request.Name.Trim(),
            Description = BomRules.NormalizeOptionalText(request.Description),
            DefaultCalculationMethod = request.DefaultCalculationMethod,
            IsRecoverable = request.IsRecoverable,
            IsActive = true,
            CreatedDate = DateTime.Now,
            CreatedBy = employeeId
        };
        await _dbContext.ManufacturingLossTypes.AddAsync(entity, cancellationToken);
        _dbContext.AuditLogs.Add(BomAudit.Create(
            companyId,
            employeeId,
            "manufacturing_loss_types",
            entity.ManufacturingLossTypeId,
            "CreateManufacturingLossType",
            new { entity.ExternalId, entity.Name, entity.DefaultCalculationMethod },
            actionType: AuditActionType.Create));
        await _dbContext.SaveChangesAsync(cancellationToken);
        return OperationResult<ManufacturingLossTypeDto>.Ok(ManufacturingLossTypeMapper.ToDto(entity));
    }
}
