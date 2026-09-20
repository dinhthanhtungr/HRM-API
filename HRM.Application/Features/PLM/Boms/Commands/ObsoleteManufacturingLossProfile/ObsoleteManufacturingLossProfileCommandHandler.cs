using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Application.Features.PLM.Boms.Mappers;
using HRM.Application.Features.PLM.Boms.Services;
using HRM.Domain.Enums.Boms;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Boms.Commands.ObsoleteManufacturingLossProfile;

internal sealed class ObsoleteManufacturingLossProfileCommandHandler
    : IRequestHandler<ObsoleteManufacturingLossProfileCommand, OperationResult<ManufacturingLossProfileDto>>
{
    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public ObsoleteManufacturingLossProfileCommandHandler(IPLMWriteDbContext dbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<OperationResult<ManufacturingLossProfileDto>> Handle(
        ObsoleteManufacturingLossProfileCommand command,
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
        if (profile.Status != ManufacturingLossProfileStatus.Released)
        {
            return OperationResult<ManufacturingLossProfileDto>.Fail("Only Released loss profiles can be obsoleted.");
        }

        profile.Status = ManufacturingLossProfileStatus.Obsolete;
        profile.UpdatedDate = DateTime.Now;
        profile.UpdatedBy = employeeId;
        _dbContext.AuditLogs.Add(BomAudit.Create(
            companyId, employeeId, "manufacturing_loss_profiles", profile.ManufacturingLossProfileId,
            "ObsoleteManufacturingLossProfile", new { profile.ExternalId }));
        await _dbContext.SaveChangesAsync(cancellationToken);
        return OperationResult<ManufacturingLossProfileDto>.Ok(ManufacturingLossProfileMapper.ToDto(profile));
    }
}
