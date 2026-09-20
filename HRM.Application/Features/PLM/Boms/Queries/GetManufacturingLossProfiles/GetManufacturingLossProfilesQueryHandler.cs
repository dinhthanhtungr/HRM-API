using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.PLM.Boms.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Boms.Queries.GetManufacturingLossProfiles;

internal sealed class GetManufacturingLossProfilesQueryHandler
    : IRequestHandler<GetManufacturingLossProfilesQuery, IReadOnlyList<ManufacturingLossProfileSummaryDto>>
{
    private readonly IPLMReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public GetManufacturingLossProfilesQueryHandler(IPLMReadDbContext dbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<ManufacturingLossProfileSummaryDto>> Handle(
        GetManufacturingLossProfilesQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty)
        {
            return [];
        }

        var query = _dbContext.ManufacturingLossProfiles.AsNoTracking()
            .Where(x => x.CompanyId == companyId);
        if (request.Status.HasValue)
        {
            query = query.Where(x => x.Status == request.Status.Value);
        }

        return await query
            .OrderBy(x => x.ExternalId)
            .Select(x => new ManufacturingLossProfileSummaryDto
            {
                ManufacturingLossProfileId = x.ManufacturingLossProfileId,
                Code = x.ExternalId,
                Name = x.Name,
                Status = x.Status,
                EffectiveFrom = x.EffectiveFrom,
                EffectiveTo = x.EffectiveTo,
                Description = x.Description,
                ActiveRuleCount = x.Rules.Count(rule => rule.IsActive)
            })
            .ToListAsync(cancellationToken);
    }
}
