using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.DevAndQA.ProductInspections.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.DevAndQA.ProductInspections.Queries.GetProductCoaSources;

internal sealed class GetProductCoaSourcesQueryHandler
    : IRequestHandler<GetProductCoaSourcesQuery, OperationResult<PagedResult<ProductCoaSourceDto>>>
{
    private readonly IPLMReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public GetProductCoaSourcesQueryHandler(IPLMReadDbContext dbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<OperationResult<PagedResult<ProductCoaSourceDto>>> Handle(
        GetProductCoaSourcesQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty)
            return OperationResult<PagedResult<ProductCoaSourceDto>>.Fail("Current company is invalid.");

        var query = _dbContext.MfgProductionOrders
            .AsNoTracking()
            .Where(order => order.CompanyId == companyId && order.IsActive &&
                order.ProductionSelectVersions.Any(version =>
                    version.ValidTo == null && version.ManufacturingFormula != null &&
                    version.ManufacturingFormula.IsActive));

        if (request.NormalizedKeyword is { } keyword)
        {
            var pattern = $"%{keyword}%";
            query = query.Where(order =>
                EF.Functions.Like(order.ExternalId, pattern) ||
                (order.ProductExternalIdSnapshot != null && EF.Functions.Like(order.ProductExternalIdSnapshot, pattern)) ||
                (order.ProductNameSnapshot != null && EF.Functions.Like(order.ProductNameSnapshot, pattern)) ||
                (order.Product.ColourCode != null && EF.Functions.Like(order.Product.ColourCode, pattern)) ||
                order.ProductionSelectVersions.Any(version =>
                    version.ValidTo == null && version.ManufacturingFormula != null &&
                    EF.Functions.Like(version.ManufacturingFormula.ExternalId, pattern)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(order => order.CreatedDate)
            .ThenByDescending(order => order.MfgProductionOrderId)
            .Skip((request.NormalizedPageNumber - 1) * request.NormalizedPageSize)
            .Take(request.NormalizedPageSize)
            .Select(order => new
            {
                order.MfgProductionOrderId,
                BatchId = order.ProductionSelectVersions
                    .Where(version => version.ValidTo == null && version.ManufacturingFormula != null)
                    .OrderByDescending(version => version.ValidFrom)
                    .Select(version => version.ManufacturingFormula!.ExternalId)
                    .FirstOrDefault(),
                order.BagType,
                order.TotalQuantityRequest,
                order.ProductId,
                ProductExternalId = order.ProductExternalIdSnapshot ?? order.Product.ColourCode,
                ProductName = order.ProductNameSnapshot ?? order.Product.Name,
                order.Product.ColourCode,
                order.Product.ExpiryType,
                ProductStandardId = _dbContext.ProductStandards
                    .Where(standard => standard.ProductId == order.ProductId &&
                        standard.CompanyId == companyId && standard.IsActive)
                    .OrderByDescending(standard => standard.CreatedDate)
                    .Select(standard => (Guid?)standard.Id)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var batchLookupIds = rows
            .SelectMany(x => BuildBatchCandidates(x.BatchId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var printRows = batchLookupIds.Length == 0
            ? []
            : await _dbContext.HistoryPrintLabelForAlls
                .AsNoTracking()
                .Where(x => x.ExternalId != null && batchLookupIds.Contains(x.ExternalId))
                .Select(x => new { x.ExternalId, x.CreatedAt })
                .ToListAsync(cancellationToken);

        var printDates = printRows
            .GroupBy(x => x.ExternalId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<DateTime>)group.OrderByDescending(x => x.CreatedAt).Select(x => x.CreatedAt).ToList(),
                StringComparer.OrdinalIgnoreCase);

        var items = rows.Select(row => new ProductCoaSourceDto
        {
            MfgProductionOrderId = row.MfgProductionOrderId,
            ProductStandardId = row.ProductStandardId,
            BatchId = row.BatchId,
            LabelPrintDates = BuildBatchCandidates(row.BatchId)
                .Where(printDates.ContainsKey)
                .SelectMany(candidate => printDates[candidate])
                .OrderByDescending(date => date)
                .ToList(),
            ExpiryType = row.ExpiryType,
            ProductPackage = row.BagType,
            TotalQuantityRequest = row.TotalQuantityRequest,
            ProductId = row.ProductId,
            ProductExternalId = row.ProductExternalId,
            ProductName = row.ProductName,
            ColourCode = row.ColourCode
        }).ToList();

        return OperationResult<PagedResult<ProductCoaSourceDto>>.Ok(
            new PagedResult<ProductCoaSourceDto>(items, totalCount, request.NormalizedPageNumber, request.NormalizedPageSize));
    }

    private static string[] BuildBatchCandidates(string? batchId)
    {
        if (string.IsNullOrWhiteSpace(batchId)) return [];
        var value = batchId.Trim();
        var alternate = value.StartsWith("VA", StringComparison.OrdinalIgnoreCase)
            ? value[2..]
            : $"VA{value}";
        return [value, alternate];
    }
}
