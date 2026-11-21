using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Domain.Enums.Boms;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Boms.Queries.GetBoms;

internal sealed class GetBomsQueryHandler
    : IRequestHandler<GetBomsQuery, IReadOnlyList<BomListItemDto>>
{
    private readonly IPLMReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public GetBomsQueryHandler(
        IPLMReadDbContext dbContext,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<BomListItemDto>> Handle(
        GetBomsQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty)
        {
            return [];
        }

        return await BomSearchQueryRules.Build(_dbContext, companyId, request.ProductId, request.BomType, request.Keyword, request, out _)
            .OrderBy(x => x.ExternalId)
            .Select(x => new BomListItemDto
            {
                BomDefinitionId = x.BomDefinitionId,
                ProductId = x.ProductId,
                Code = x.ExternalId,
                Name = x.Name,
                BomType = x.BomType,
                IsActive = x.IsActive,
                VersionCount = x.Versions.Count
            })
            .ToListAsync(cancellationToken);
    }
}
