using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Boms.Services;
using HRM.Domain.Enums.Boms;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Boms.Commands.ObsoleteBomVersion;

internal sealed class ObsoleteBomVersionCommandHandler : IRequestHandler<ObsoleteBomVersionCommand, OperationResult>
{
    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public ObsoleteBomVersionCommandHandler(IPLMWriteDbContext dbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<OperationResult> Handle(ObsoleteBomVersionCommand command, CancellationToken cancellationToken)
    {
        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty ||
            _currentUser.EmployeeId is not { } employeeId || employeeId == Guid.Empty)
        {
            return OperationResult.Fail("Current company or employee is invalid.");
        }

        var version = await _dbContext.BomVersions
            .Include(x => x.BomDefinition)
            .FirstOrDefaultAsync(
                x => x.BomVersionId == command.BomVersionId && x.BomDefinition.CompanyId == companyId,
                cancellationToken);
        if (version is null)
        {
            return OperationResult.Fail("BOM version was not found.");
        }
        if (version.Status != BomVersionStatus.Released)
        {
            return OperationResult.Fail("Only Released BOM versions can be made obsolete.");
        }

        var isCurrentStandard = await _dbContext.ProductStandardBomVersions
            .AsNoTracking()
            .AnyAsync(x => x.BomVersionId == version.BomVersionId && x.ValidTo == null, cancellationToken);
        if (isCurrentStandard)
        {
            return OperationResult.Fail("The current standard BOM cannot be made obsolete.");
        }

        version.Status = BomVersionStatus.Obsolete;
        _dbContext.AuditLogs.Add(BomAudit.Create(
            companyId, employeeId, "bom_versions", version.BomVersionId, "obsolete",
            new { version.VersionNo }, command.Reason));
        await _dbContext.SaveChangesAsync(cancellationToken);
        return OperationResult.Ok("BOM version made obsolete successfully.");
    }
}
