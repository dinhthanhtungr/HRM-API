using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.DevAndQA.ProductInspections.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.DevAndQA.ProductInspections.Queries.GetProductInspections;

internal sealed class GetProductInspectionsQueryHandler
    : IRequestHandler<GetProductInspectionsQuery, OperationResult<PagedResult<ProductInspectionSummaryDto>>>
{
    private readonly IPLMReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public GetProductInspectionsQueryHandler(IPLMReadDbContext dbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<OperationResult<PagedResult<ProductInspectionSummaryDto>>> Handle(
        GetProductInspectionsQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty)
            return OperationResult<PagedResult<ProductInspectionSummaryDto>>.Fail("Current company is invalid.");

        var query = _dbContext.ProductInspections
            .AsNoTracking()
            .Where(x => x.ProductStandardId.HasValue &&
                _dbContext.ProductStandards.Any(s =>
                    s.Id == x.ProductStandardId.Value && s.CompanyId == companyId));

        if (request.NormalizedKeyword is { } keyword)
        {
            var pattern = $"%{keyword}%";
            query = query.Where(x =>
                (x.ExternalId != null && EF.Functions.Like(x.ExternalId, pattern)) ||
                (x.BatchId != null && EF.Functions.Like(x.BatchId, pattern)) ||
                (x.ProductCode != null && EF.Functions.Like(x.ProductCode, pattern)) ||
                (x.ProductName != null && EF.Functions.Like(x.ProductName, pattern)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.CreateDate)
            .ThenByDescending(x => x.Id)
            .Skip((request.NormalizedPageNumber - 1) * request.NormalizedPageSize)
            .Take(request.NormalizedPageSize)
            .Select(x => new ProductInspectionSummaryDto
            {
                Id = x.Id,
                ExternalId = x.ExternalId,
                ProductCode = x.ProductCode,
                ProductName = x.ProductName,
                BatchId = x.BatchId,
                Result = x.DeliveryAccepted,
                Types = x.Types,
                CreateDate = x.CreateDate,
                CreatedBy = x.CreatedBy
            })
            .ToListAsync(cancellationToken);

        return OperationResult<PagedResult<ProductInspectionSummaryDto>>.Ok(
            new PagedResult<ProductInspectionSummaryDto>(
                items,
                totalCount,
                request.NormalizedPageNumber,
                request.NormalizedPageSize));
    }
}
