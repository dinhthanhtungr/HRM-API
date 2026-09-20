using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Application.Features.PLM.Boms.Services;
using HRM.Domain.Enums.Boms;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Boms.Commands.ApplyManufacturingLossProfile;

internal sealed class ApplyManufacturingLossProfileCommandHandler
    : IRequestHandler<ApplyManufacturingLossProfileCommand, OperationResult<ManufacturingLossProfileApplicationDto>>
{
    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public ApplyManufacturingLossProfileCommandHandler(IPLMWriteDbContext dbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<OperationResult<ManufacturingLossProfileApplicationDto>> Handle(
        ApplyManufacturingLossProfileCommand command,
        CancellationToken cancellationToken)
    {
        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty ||
            _currentUser.EmployeeId is not { } employeeId || employeeId == Guid.Empty ||
            command.ProfileId == Guid.Empty)
        {
            return OperationResult<ManufacturingLossProfileApplicationDto>.Fail("Current company, employee, or ProfileId is invalid.");
        }

        var version = await _dbContext.BomVersions
            .Include(x => x.BomDefinition)
            .Include(x => x.Items)
            .Include(x => x.ManufacturingStages)
            .Include(x => x.ManufacturingStageTransitions)
            .Include(x => x.LossRules)
            .FirstOrDefaultAsync(x => x.BomVersionId == command.BomVersionId &&
                                      x.BomDefinition.CompanyId == companyId &&
                                      x.BomDefinition.BomType == BomType.Manufacturing,
                cancellationToken);
        if (version is null)
        {
            return OperationResult<ManufacturingLossProfileApplicationDto>.Fail("Manufacturing BOM version was not found.");
        }

        var profile = await _dbContext.ManufacturingLossProfiles
            .Include(x => x.Rules).ThenInclude(x => x.LossType)
            .FirstOrDefaultAsync(x => x.ManufacturingLossProfileId == command.ProfileId &&
                                      x.CompanyId == companyId,
                cancellationToken);
        if (profile is null)
        {
            return OperationResult<ManufacturingLossProfileApplicationDto>.Fail("Loss profile was not found.");
        }

        var resolution = ManufacturingLossProfileApplicationResolver.Resolve(version, profile, DateTime.Now);
        if (resolution.Error is not null)
        {
            return OperationResult<ManufacturingLossProfileApplicationDto>.Fail(resolution.Error);
        }

        _dbContext.ManufacturingBomLossRules.RemoveRange(version.LossRules);
        await _dbContext.ManufacturingBomLossRules.AddRangeAsync(resolution.Rules, cancellationToken);
        _dbContext.AuditLogs.Add(BomAudit.Create(
            companyId, employeeId, "bom_versions", version.BomVersionId,
            "ApplyManufacturingLossProfile",
            new
            {
                ProfileId = profile.ManufacturingLossProfileId,
                profile.ExternalId,
                ReplacedRuleCount = version.LossRules.Count,
                AppliedRuleCount = resolution.Rules.Count
            }));
        await _dbContext.SaveChangesAsync(cancellationToken);

        return OperationResult<ManufacturingLossProfileApplicationDto>.Ok(
            ManufacturingLossProfileApplicationResolver.ToDto(
                version.BomVersionId, profile, resolution.Rules, isPreview: false));
    }
}
