using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Application.Features.PLM.Boms.Mappers;
using HRM.Application.Features.PLM.Boms.Services;
using HRM.Domain.Enums.Boms;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Boms.Commands.ReleaseManufacturingLossProfile;

internal sealed class ReleaseManufacturingLossProfileCommandHandler
    : IRequestHandler<ReleaseManufacturingLossProfileCommand, OperationResult<ManufacturingLossProfileDto>>
{
    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public ReleaseManufacturingLossProfileCommandHandler(IPLMWriteDbContext dbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<OperationResult<ManufacturingLossProfileDto>> Handle(
        ReleaseManufacturingLossProfileCommand command,
        CancellationToken cancellationToken)
    {
        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty ||
            _currentUser.EmployeeId is not { } employeeId || employeeId == Guid.Empty)
        {
            return OperationResult<ManufacturingLossProfileDto>.Fail("Current company or employee is invalid.");
        }

        var profile = await _dbContext.ManufacturingLossProfiles
            .Include(x => x.Rules).ThenInclude(x => x.LossType)
            .FirstOrDefaultAsync(x => x.ManufacturingLossProfileId == command.ProfileId &&
                                      x.CompanyId == companyId,
                cancellationToken);
        if (profile is null)
        {
            return OperationResult<ManufacturingLossProfileDto>.Fail("Loss profile was not found.");
        }
        if (profile.Status != ManufacturingLossProfileStatus.Draft)
        {
            return OperationResult<ManufacturingLossProfileDto>.Fail("Only Draft loss profiles can be released.");
        }
        if (!profile.Rules.Any(x => x.IsActive))
        {
            return OperationResult<ManufacturingLossProfileDto>.Fail("A loss profile requires at least one active rule before release.");
        }
        if (profile.Rules.Any(x => x.IsActive && (!x.LossType.IsActive || x.LossType.CompanyId != companyId)))
        {
            return OperationResult<ManufacturingLossProfileDto>.Fail("Every active profile rule must use an active loss type from your company.");
        }

        profile.Status = ManufacturingLossProfileStatus.Released;
        profile.ReleasedDate = DateTime.Now;
        profile.ReleasedBy = employeeId;
        profile.UpdatedDate = DateTime.Now;
        profile.UpdatedBy = employeeId;
        _dbContext.AuditLogs.Add(BomAudit.Create(
            companyId, employeeId, "manufacturing_loss_profiles", profile.ManufacturingLossProfileId,
            "ReleaseManufacturingLossProfile", new { profile.ExternalId, profile.ReleasedDate }));
        await _dbContext.SaveChangesAsync(cancellationToken);
        return OperationResult<ManufacturingLossProfileDto>.Ok(ManufacturingLossProfileMapper.ToDto(profile));
    }
}
