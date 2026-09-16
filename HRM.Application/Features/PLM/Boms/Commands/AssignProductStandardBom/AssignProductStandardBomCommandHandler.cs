using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Application.Features.PLM.Boms.Rules;
using HRM.Application.Features.PLM.Boms.Services;
using HRM.Domain.Entities.BomSchema;
using HRM.Domain.Enums.Boms;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Boms.Commands.AssignProductStandardBom;

internal sealed class AssignProductStandardBomCommandHandler
    : IRequestHandler<AssignProductStandardBomCommand, OperationResult<ProductStandardBomVersionDto>>
{
    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public AssignProductStandardBomCommandHandler(IPLMWriteDbContext dbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<OperationResult<ProductStandardBomVersionDto>> Handle(
        AssignProductStandardBomCommand command,
        CancellationToken cancellationToken)
    {
        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty ||
            _currentUser.EmployeeId is not { } employeeId || employeeId == Guid.Empty)
        {
            return OperationResult<ProductStandardBomVersionDto>.Fail("Current company or employee is invalid.");
        }
        if (command.ProductId == Guid.Empty || command.Request.BomVersionId == Guid.Empty ||
            command.Request.ValidFrom == default)
        {
            return OperationResult<ProductStandardBomVersionDto>.Fail("ProductId, BomVersionId, and ValidFrom are required.");
        }

        var version = await _dbContext.BomVersions
            .AsNoTracking()
            .Where(x =>
                x.BomVersionId == command.Request.BomVersionId &&
                x.Status == BomVersionStatus.Released &&
                x.BomDefinition.CompanyId == companyId &&
                x.BomDefinition.ProductId == command.ProductId &&
                x.BomDefinition.BomType == BomType.Manufacturing &&
                x.BomDefinition.IsActive)
            .Select(x => new
            {
                x.BomVersionId,
                x.BomDefinitionId,
                x.VersionNo,
                x.BomDefinition.Code,
                x.BomDefinition.Name
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (version is null)
        {
            return OperationResult<ProductStandardBomVersionDto>.Fail(
                "Released Manufacturing BOM was not found for this Product and company.");
        }

        var current = await _dbContext.ProductStandardBomVersions
            .FirstOrDefaultAsync(
                x => x.CompanyId == companyId && x.ProductId == command.ProductId && x.ValidTo == null,
                cancellationToken);
        if (current is not null)
        {
            if (current.BomVersionId == version.BomVersionId)
            {
                return OperationResult<ProductStandardBomVersionDto>.Fail("This BOM version is already the current standard.");
            }
            if (command.Request.ValidFrom <= current.ValidFrom)
            {
                return OperationResult<ProductStandardBomVersionDto>.Fail(
                    "ValidFrom must be later than the current standard BOM ValidFrom.");
            }

            current.ValidTo = command.Request.ValidFrom;
            current.ClosedDate = DateTime.Now;
            current.ClosedBy = employeeId;
        }

        var assignment = new ProductStandardBomVersion
        {
            ProductStandardBomVersionId = Guid.CreateVersion7(),
            CompanyId = companyId,
            ProductId = command.ProductId,
            BomVersionId = version.BomVersionId,
            ValidFrom = command.Request.ValidFrom,
            Note = BomRules.NormalizeOptionalText(command.Request.Note),
            CreatedDate = DateTime.Now,
            CreatedBy = employeeId
        };
        await _dbContext.ProductStandardBomVersions.AddAsync(assignment, cancellationToken);
        _dbContext.AuditLogs.Add(BomAudit.Create(
            companyId, employeeId, "product_standard_bom_versions", 
            assignment.ProductStandardBomVersionId, "assign-standard",
            new { command.ProductId, version.BomVersionId, command.Request.ValidFrom }));
        await _dbContext.SaveChangesAsync(cancellationToken);

        return OperationResult<ProductStandardBomVersionDto>.Ok(new ProductStandardBomVersionDto
        {
            ProductStandardBomVersionId = assignment.ProductStandardBomVersionId,
            ProductId = assignment.ProductId,
            BomDefinitionId = version.BomDefinitionId,
            BomVersionId = version.BomVersionId,
            BomCode = version.Code,
            BomName = version.Name,
            VersionNo = version.VersionNo,
            ValidFrom = assignment.ValidFrom,
            ValidTo = assignment.ValidTo,
            Note = assignment.Note
        });
    }
}
