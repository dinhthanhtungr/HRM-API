using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.PLM.Boms.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Boms.Queries.GetProductStandardBoms;

internal sealed class GetProductStandardBomsQueryHandler
    : IRequestHandler<GetProductStandardBomsQuery, IReadOnlyList<ProductStandardBomVersionDto>>
{
    private readonly IPLMReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public GetProductStandardBomsQueryHandler(IPLMReadDbContext dbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<ProductStandardBomVersionDto>> Handle(
        GetProductStandardBomsQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty || request.ProductId == Guid.Empty)
        {
            return [];
        }

        var query = _dbContext.ProductStandardBomVersions
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.ProductId == request.ProductId);
        if (!request.History)
        {
            var at = request.At ?? DateTime.Now;
            query = query.Where(x => x.ValidFrom <= at && (x.ValidTo == null || x.ValidTo > at));
        }

        return await query
            .OrderByDescending(x => x.ValidFrom)
            .Select(x => new ProductStandardBomVersionDto
            {
                ProductStandardBomVersionId = x.ProductStandardBomVersionId,
                ProductId = x.ProductId,
                BomDefinitionId = x.BomVersion.BomDefinitionId,
                BomVersionId = x.BomVersionId,
                BomCode = x.BomVersion.BomDefinition.ExternalId,
                BomName = x.BomVersion.BomDefinition.Name,
                VersionNo = x.BomVersion.VersionNo,
                ValidFrom = x.ValidFrom,
                ValidTo = x.ValidTo,
                Note = x.Note
            })
            .ToListAsync(cancellationToken);
    }
}
