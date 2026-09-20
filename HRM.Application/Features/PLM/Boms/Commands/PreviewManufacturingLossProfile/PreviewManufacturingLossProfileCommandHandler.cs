using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Application.Features.PLM.Boms.Services;
using HRM.Domain.Enums.Boms;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Boms.Commands.PreviewManufacturingLossProfile;

internal sealed class PreviewManufacturingLossProfileCommandHandler
    : IRequestHandler<PreviewManufacturingLossProfileCommand, OperationResult<ManufacturingLossProfileApplicationDto>>
{
    private readonly IPLMReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public PreviewManufacturingLossProfileCommandHandler(IPLMReadDbContext dbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<OperationResult<ManufacturingLossProfileApplicationDto>> Handle(
        PreviewManufacturingLossProfileCommand command,
        CancellationToken cancellationToken)
    {
        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty || command.ProfileId == Guid.Empty)
        {
            return OperationResult<ManufacturingLossProfileApplicationDto>.Fail("Current company or ProfileId is invalid.");
        }

        var version = await LoadVersionAsync(command.BomVersionId, companyId, cancellationToken);
        if (version is null)
        {
            return OperationResult<ManufacturingLossProfileApplicationDto>.Fail("Manufacturing BOM version was not found.");
        }
        var profile = await LoadProfileAsync(command.ProfileId, companyId, cancellationToken);
        if (profile is null)
        {
            return OperationResult<ManufacturingLossProfileApplicationDto>.Fail("Loss profile was not found.");
        }

        var resolution = ManufacturingLossProfileApplicationResolver.Resolve(version, profile, DateTime.Now);
        return resolution.Error is not null
            ? OperationResult<ManufacturingLossProfileApplicationDto>.Fail(resolution.Error)
            : OperationResult<ManufacturingLossProfileApplicationDto>.Ok(
                ManufacturingLossProfileApplicationResolver.ToDto(
                    version.BomVersionId, profile, resolution.Rules, isPreview: true));
    }

    private Task<HRM.Domain.Entities.BomSchema.BomVersion?> LoadVersionAsync(
        Guid versionId,
        Guid companyId,
        CancellationToken cancellationToken)
        => _dbContext.BomVersions.AsNoTracking()
            .Include(x => x.BomDefinition)
            .Include(x => x.Items)
            .Include(x => x.ManufacturingStages)
            .Include(x => x.ManufacturingStageTransitions)
            .FirstOrDefaultAsync(x => x.BomVersionId == versionId &&
                                      x.BomDefinition.CompanyId == companyId &&
                                      x.BomDefinition.BomType == BomType.Manufacturing,
                cancellationToken);

    private Task<HRM.Domain.Entities.BomSchema.ManufacturingLossProfile?> LoadProfileAsync(
        Guid profileId,
        Guid companyId,
        CancellationToken cancellationToken)
        => _dbContext.ManufacturingLossProfiles.AsNoTracking()
            .Include(x => x.Rules).ThenInclude(x => x.LossType)
            .FirstOrDefaultAsync(x => x.ManufacturingLossProfileId == profileId &&
                                      x.CompanyId == companyId,
                cancellationToken);
}
