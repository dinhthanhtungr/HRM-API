using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.PLM.Boms.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Boms.Queries.GetBomDefinition;

internal sealed class GetBomDefinitionQueryHandler
    : IRequestHandler<GetBomDefinitionQuery, BomDefinitionDetailDto?>
{
    private readonly IPLMReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public GetBomDefinitionQueryHandler(IPLMReadDbContext dbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<BomDefinitionDetailDto?> Handle(
        GetBomDefinitionQuery request,
        CancellationToken cancellationToken)
    {
        if (request.BomDefinitionId == Guid.Empty ||
            _currentUser.CompanyId is not { } companyId || companyId == Guid.Empty)
        {
            return null;
        }

        return await _dbContext.BomDefinitions
            .AsNoTracking()
            .Where(x =>
                x.BomDefinitionId == request.BomDefinitionId &&
                x.CompanyId == companyId &&
                (!request.ExpectedBomType.HasValue || x.BomType == request.ExpectedBomType))
            .Select(x => new BomDefinitionDetailDto
            {
                BomDefinitionId = x.BomDefinitionId,
                ProductId = x.ProductId,
                Code = x.ExternalId,
                Name = x.Name,
                BomType = x.BomType,
                Description = x.Description,
                IsActive = x.IsActive,
                Versions = x.Versions
                    .OrderByDescending(v => v.VersionNo)
                    .Select(v => new BomVersionSummaryDto
                    {
                        BomVersionId = v.BomVersionId,
                        VersionNo = v.VersionNo,
                        Status = v.Status,
                        EffectiveFrom = v.EffectiveFrom,
                        EffectiveTo = v.EffectiveTo,
                        CreatedDate = v.CreatedDate,
                        ReleasedDate = v.ReleasedDate
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);
    }
}
