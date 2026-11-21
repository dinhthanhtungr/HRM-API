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

        var smartSearch = HRM.Application.Commons.Searching.SmartSearch.For(query, fields => [
            fields.Code("formulaCode", x => x.Formula.ExternalId, "Công thức", "Formula", "VU"),
            fields.Code("colourCode", x => x.Formula.Product.ColourCode, "ColorCode", "Mã màu"),
            fields.Name("productName", x => x.Formula.Product.Name, "Tên sản phẩm", "Product name"),
            fields.Codes("sampleRequestCode", x => x.Formula.Product.SampleRequests.Where(s => s.IsActive && s.CompanyId == companyId),
                s => s.ExternalId, "TP Code", "Mã TP")
        ]);
        query = smartSearch.ApplyRequest(query, request.NormalizedKeyword, request, out var smartKeyword);
        if (smartKeyword is { } keyword)
        {
            query = query.Where(x =>
                EF.Functions.ILike(x.Formula.ExternalId, $"%{keyword}%") ||
                EF.Functions.ILike(x.Formula.Name, PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter) ||
                (x.Formula.Product.Name != null && EF.Functions.ILike(x.Formula.Product.Name, PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter)) ||
                (x.Formula.Product.ColourCode != null && EF.Functions.ILike(x.Formula.Product.ColourCode, PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter)) ||
                x.Formula.Product.SampleRequests.Any(sampleRequest =>
                    sampleRequest.IsActive &&
                    sampleRequest.CompanyId == companyId &&
                    EF.Functions.ILike(sampleRequest.ExternalId, PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter)));
        }

        if (await smartSearch.SuggestionsAsync<ManufacturingVUFormulaListItemDto>(query, request, cancellationToken) is { } suggestions)
            return OperationResult<PagedResult<ManufacturingVUFormulaListItemDto>>.Ok(suggestions);
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
