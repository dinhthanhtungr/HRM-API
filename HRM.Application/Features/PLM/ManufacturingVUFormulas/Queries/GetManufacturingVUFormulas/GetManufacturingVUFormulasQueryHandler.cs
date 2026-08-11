using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.PLM.ManufacturingVUFormulas.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.ManufacturingVUFormulas.Queries.GetManufacturingVUFormulas;

internal sealed class GetManufacturingVUFormulasQueryHandler
    : IRequestHandler<
        GetManufacturingVUFormulasQuery,
        OperationResult<PagedResult<ManufacturingVUFormulaListItemDto>>>
{
    private readonly IPLMReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public GetManufacturingVUFormulasQueryHandler(
        IPLMReadDbContext dbContext,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<OperationResult<PagedResult<ManufacturingVUFormulaListItemDto>>> Handle(
        GetManufacturingVUFormulasQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty)
        {
            return OperationResult<PagedResult<ManufacturingVUFormulaListItemDto>>
                .Fail("Current company is invalid.");
        }

        var query = _dbContext.ManufacturingVUFormulas
            .AsNoTracking()
            .Where(x => x.Formula.Product.CompanyId == companyId);

        if (request.ProductId is { } productId && productId != Guid.Empty)
        {
            query = query.Where(x => x.Formula.ProductId == productId);
        }

        if (request.Status is { } status)
        {
            query = query.Where(x => x.status == status);
        }

        if (request.NormalizedKeyword is { } keyword)
        {
            query = query.Where(x =>
                x.Formula.ExternalId.Contains(keyword) ||
                x.Formula.Name.Contains(keyword) ||
                (x.Formula.Product.Name != null && x.Formula.Product.Name.Contains(keyword)) ||
                (x.Formula.Product.ColourCode != null && x.Formula.Product.ColourCode.Contains(keyword)));
        }

        query = request.NormalizedSortBy?.ToLowerInvariant() switch
        {
            "formulaexternalid" => request.SortDescending
                ? query.OrderByDescending(x => x.Formula.ExternalId)
                : query.OrderBy(x => x.Formula.ExternalId),
            "productname" => request.SortDescending
                ? query.OrderByDescending(x => x.Formula.Product.Name)
                : query.OrderBy(x => x.Formula.Product.Name),
            "status" => request.SortDescending
                ? query.OrderByDescending(x => x.status)
                : query.OrderBy(x => x.status),
            _ => request.SortDescending
                ? query.OrderByDescending(x => x.CreatedDate)
                    .ThenByDescending(x => x.ManufacturingVUFormulaId)
                : query.OrderBy(x => x.CreatedDate)
                    .ThenBy(x => x.ManufacturingVUFormulaId)
        };

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((request.NormalizedPageNumber - 1) * request.NormalizedPageSize)
            .Take(request.NormalizedPageSize)
            .Select(x => new ManufacturingVUFormulaListItemDto
            {
                ManufacturingVUFormulaId = x.ManufacturingVUFormulaId,
                FormulaId = x.FormulaId,
                FormulaExternalId = x.Formula.ExternalId,
                FormulaName = x.Formula.Name,
                ProductId = x.Formula.ProductId,
                ProductName = x.Formula.Product.Name,
                ColourCode = x.Formula.Product.ColourCode,
                TotalProductionQuantity = x.TotalProductionQuantity,
                NumOfBatches = x.NumOfBatches,
                Status = x.status,
                CreatedByName = x.CreatedByNavigation != null
                    ? x.CreatedByNavigation.FullName
                    : null,
                CreatedDate = x.CreatedDate
            })
            .ToListAsync(cancellationToken);

        return OperationResult<PagedResult<ManufacturingVUFormulaListItemDto>>.Ok(
            new PagedResult<ManufacturingVUFormulaListItemDto>(
                items,
                totalCount,
                request.NormalizedPageNumber,
                request.NormalizedPageSize));
    }
}
