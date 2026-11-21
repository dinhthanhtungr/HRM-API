using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Commons.Searching;
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

        var isIms = ImsAccountRestrictions.AppliesTo(_currentUser);
        var query = _dbContext.ProductInspections
            .AsNoTracking()
            .ForCompany(_dbContext, companyId);

        var smartSearch = SmartSearch.For(query, fields => [
            fields.Code("inspectionCode", x => x.ExternalId, "Mã kiểm tra", "Inspection code"),
            fields.Code("batchNumber", x => x.BatchId, "Số lô", "Batch"),
            fields.Code("productCode", x => x.ProductCode, "Mã sản phẩm", "Product code"),
            fields.Name("productName", x => x.ProductName, "Tên sản phẩm", "Product name")
        ]);
        query = smartSearch.ApplyRequest(query, request.NormalizedKeyword, request, out var smartKeyword);
        if (smartKeyword is { } keyword)
        {
            var pattern = $"%{keyword}%";
            query = query.Where(x =>
                (x.ExternalId != null && EF.Functions.Like(x.ExternalId, pattern)) ||
                (x.BatchId != null && EF.Functions.Like(x.BatchId, pattern)) ||
                (x.ProductCode != null && EF.Functions.Like(x.ProductCode, pattern)) ||
                (x.ProductName != null && EF.Functions.Like(x.ProductName, pattern)));
        }

        if (await smartSearch.SuggestionsAsync<ProductInspectionSummaryDto>(query, request, cancellationToken) is { } suggestions)
            return OperationResult<PagedResult<ProductInspectionSummaryDto>>.Ok(suggestions);
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
                Types = isIms ? null : x.Types,
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
