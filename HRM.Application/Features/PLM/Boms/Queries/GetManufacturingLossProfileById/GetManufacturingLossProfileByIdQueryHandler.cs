using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Application.Features.PLM.Boms.Mappers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Boms.Queries.GetManufacturingLossProfileById;

internal sealed class GetManufacturingLossProfileByIdQueryHandler
    : IRequestHandler<GetManufacturingLossProfileByIdQuery, ManufacturingLossProfileDto?>
{
    private readonly IPLMReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public GetManufacturingLossProfileByIdQueryHandler(IPLMReadDbContext dbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<ManufacturingLossProfileDto?> Handle(
        GetManufacturingLossProfileByIdQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty)
        {
            return null;
        }

        var profile = await _dbContext.ManufacturingLossProfiles.AsNoTracking()
            .Include(x => x.Rules).ThenInclude(x => x.LossType)
            .FirstOrDefaultAsync(x => x.ManufacturingLossProfileId == request.ProfileId &&
                                      x.CompanyId == companyId,
                cancellationToken);
        return profile is null ? null : ManufacturingLossProfileMapper.ToDto(profile);
    }
}
