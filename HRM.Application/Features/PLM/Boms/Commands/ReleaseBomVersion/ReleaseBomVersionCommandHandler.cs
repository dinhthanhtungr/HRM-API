using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Boms.Services;
using HRM.Domain.Enums.Boms;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Boms.Commands.ReleaseBomVersion;

internal sealed class ReleaseBomVersionCommandHandler : IRequestHandler<ReleaseBomVersionCommand, OperationResult>
{
    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly BomLifecycleService _lifecycle;

    public ReleaseBomVersionCommandHandler(
        IPLMWriteDbContext dbContext,
        ICurrentUser currentUser,
        BomLifecycleService lifecycle)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _lifecycle = lifecycle;
    }

    public async Task<OperationResult> Handle(ReleaseBomVersionCommand command, CancellationToken cancellationToken)
    {
        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty ||
            _currentUser.EmployeeId is not { } employeeId || employeeId == Guid.Empty)
        {
            return OperationResult.Fail("Current company or employee is invalid.");
        }

        var version = await _dbContext.BomVersions
            .Include(x => x.BomDefinition)
            .Include(x => x.Items)
            .Include(x => x.ManufacturingStages)
            .Include(x => x.LossRules)
            .FirstOrDefaultAsync(
                x => x.BomVersionId == command.BomVersionId && x.BomDefinition.CompanyId == companyId,
                cancellationToken);
        if (version is null)
        {
            return OperationResult.Fail("BOM version was not found.");
        }

        var error = await _lifecycle.ValidateForReleaseAsync(version, companyId, cancellationToken);
        if (error is not null)
        {
            return OperationResult.Fail(error);
        }

        version.Status = BomVersionStatus.Released;
        version.ReleasedDate = DateTime.Now;
        version.ReleasedBy = employeeId;
        _dbContext.AuditLogs.Add(BomAudit.Create(
            companyId, employeeId, "bom_versions", version.BomVersionId, "release",
            new { version.VersionNo, version.ReleasedDate }));
        await _dbContext.SaveChangesAsync(cancellationToken);
        return OperationResult.Ok("BOM version released successfully.");
    }
}
