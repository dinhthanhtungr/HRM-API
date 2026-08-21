using HRM.Application.Abstractions.Persistence.Warehouse;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.Warehouse.Dtos;
using HRM.Application.Features.Warehouse.Helpers.Publics;
using HRM.Domain.Entities.WarehouseSchema;
using HRM.Domain.Enums.WareHouses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Warehouse.Queries.GetStockAvailable;

public sealed class GetStockAvailableQueryHandler
    : IRequestHandler<GetStockAvailableQuery, OperationResult<PagedResult<StockAvailableDto>>>
{
    private readonly IWarehouseReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public GetStockAvailableQueryHandler(IWarehouseReadDbContext dbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<OperationResult<PagedResult<StockAvailableDto>>> Handle(
        GetStockAvailableQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.CompanyId is not Guid companyId || companyId == Guid.Empty)
        {
            return OperationResult<PagedResult<StockAvailableDto>>.Fail("Current company is invalid.");
        }

        var pageNumber = request.NormalizedPageNumber;
        var pageSize = request.NormalizedPageSize;

        var items = await BuildStockAvailableItemsAsync(request, companyId, cancellationToken);
        var totalCount = items.Count;
        var pagedItems = items
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return OperationResult<PagedResult<StockAvailableDto>>.Ok(
            new PagedResult<StockAvailableDto>(pagedItems, totalCount, pageNumber, pageSize));
    }

    private IQueryable<WarehouseShelfStock> BuildShelfStockQuery(GetStockAvailableQuery request, Guid companyId)
    {
        var shelfQuery = WarehouseStockQueryHelper.ActiveShelfStocks(
            _dbContext.WarehouseShelfStocks.AsNoTracking(),
            companyId);

        var keyword = request.NormalizedKeyword;
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var sampleProductColourCodes =
                from sampleRequest in _dbContext.SampleRequests.AsNoTracking()
                join product in _dbContext.Products.AsNoTracking()
                    on sampleRequest.ProductId equals product.ProductId
                where sampleRequest.CompanyId == companyId
                      && sampleRequest.IsActive
                      && sampleRequest.ExternalId.Contains(keyword)
                      && product.CompanyId == companyId
                      && product.ColourCode != null
                      && product.ColourCode != string.Empty
                select product.ColourCode;

            var formulaProductColourCodes =
                from sampleRequest in _dbContext.SampleRequests.AsNoTracking()
                join product in _dbContext.Products.AsNoTracking()
                    on sampleRequest.ProductId equals product.ProductId
                where sampleRequest.CompanyId == companyId
                      && sampleRequest.IsActive
                      && sampleRequest.Formula != null
                      && sampleRequest.Formula.IsActive
                      && EF.Functions.ILike(sampleRequest.Formula.ExternalId, $"%{keyword}%")
                      && product.CompanyId == companyId
                      && product.ColourCode != null
                      && product.ColourCode != string.Empty
                select product.ColourCode;

            shelfQuery =
                from stock in shelfQuery
                join material in _dbContext.Materials.AsNoTracking().Where(x => x.CompanyId == companyId)
                    on stock.Code equals material.ExternalId into materialJoin
                from material in materialJoin.DefaultIfEmpty()
                join product in _dbContext.Products.AsNoTracking().Where(x => x.CompanyId == companyId)
                    on stock.Code equals product.ColourCode into productJoin
                from product in productJoin.DefaultIfEmpty()
                where (stock.Code ?? string.Empty).Contains(keyword)
                      || (stock.LotNo ?? string.Empty).Contains(keyword)
                      || (stock.LotKey ?? string.Empty).Contains(keyword)
                      || (material.Name ?? string.Empty).Contains(keyword)
                      || (product.Name ?? string.Empty).Contains(keyword)
                      || sampleProductColourCodes.Contains(stock.Code)
                      || formulaProductColourCodes.Contains(stock.Code)
                select stock;
        }

        if (request.StockTypes.HasValue)
        {
            shelfQuery = shelfQuery.Where(stock => stock.StockType == request.StockTypes.Value);
        }

        return shelfQuery;
    }

    private async Task<List<StockAvailableDto>> BuildStockAvailableItemsAsync(
        GetStockAvailableQuery request,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var shelfQuery = BuildShelfStockQuery(request, companyId);

        var items = await (
            from stock in shelfQuery
            group stock by new { stock.Code, stock.StockType } into stockGroup
            select new StockAvailableDto
            {
                ShelfStockId = stockGroup.Min(x => x.ShelfStockId),
                Code = stockGroup.Key.Code!,
                StockType = stockGroup.Key.StockType,
                TotalOnHandKg = stockGroup.Sum(x => (decimal?)x.QtyKg) ?? 0m
            })
            .OrderBy(x => x.Code)
            .ToListAsync(cancellationToken);

        if (items.Count == 0)
        {
            return items;
        }

        var materialCodes = items
            .Where(x => IsRawMaterialStockType(x.StockType))
            .Select(x => x.Code)
            .Distinct()
            .ToList();

        var productCodes = items
            .Where(x => IsFinishedGoodStockType(x.StockType))
            .Select(x => x.Code)
            .Distinct()
            .ToList();

        var materialMap = (await _dbContext.Materials
            .AsNoTracking()
            .Where(material =>
                material.CompanyId == companyId &&
                material.ExternalId != null &&
                materialCodes.Contains(material.ExternalId))
            .Select(material => new ItemNameProjection(
                material.ExternalId!,
                material.Name ?? string.Empty,
                material.Category.Name ?? string.Empty))
            .ToListAsync(cancellationToken))
            .GroupBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        var productMap = (await _dbContext.Products
            .AsNoTracking()
            .Where(product =>
                product.CompanyId == companyId &&
                product.ColourCode != null &&
                productCodes.Contains(product.ColourCode))
            .Select(product => new ItemNameProjection(
                product.ColourCode!,
                product.Name ?? string.Empty,
                product.Category != null ? product.Category.Name ?? string.Empty : string.Empty))
            .ToListAsync(cancellationToken))
            .GroupBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        var detailRows = await (
            from stock in shelfQuery
            group stock by new
            {
                stock.Code,
                stock.StockType,
                stock.ShelfStockCode,
                CompanyName = stock.Company != null ? stock.Company.Name : string.Empty,
                stock.LotNo
            }
            into stockGroup
            select new StockDetailProjection(
                stockGroup.Key.Code,
                stockGroup.Key.StockType,
                stockGroup.Key.ShelfStockCode,
                stockGroup.Key.CompanyName ?? string.Empty,
                stockGroup.Key.LotNo,
                stockGroup.Sum(x => (decimal?)x.QtyKg) ?? 0m))
            .ToListAsync(cancellationToken);

        var codes = items.Select(x => x.Code).Distinct().ToList();
        var reservedRows = await (
            from tempStock in _dbContext.WarehouseTempStocks.AsNoTracking()
            where tempStock.CompanyId == companyId
                  && codes.Contains(tempStock.Code)
                  && tempStock.ReserveStatus == ReserveStatus.Open.ToString()
            let remainQty = (tempStock.QtyRequest ?? 0m) - (tempStock.QtyUsed ?? 0m)
            where remainQty > 0m
            group remainQty by tempStock.Code into tempStockGroup
            select new
            {
                Code = tempStockGroup.Key,
                ReservedOpenKg = tempStockGroup.Sum()
            })
            .ToListAsync(cancellationToken);

        var reservedMap = reservedRows.ToDictionary(
            x => NormalizeCode(x.Code),
            x => x.ReservedOpenKg);

        var reservedVaRows = await (
            from tempStock in _dbContext.WarehouseTempStocks.AsNoTracking()
            where tempStock.CompanyId == companyId
                  && codes.Contains(tempStock.Code)
                  && tempStock.ReserveStatus == ReserveStatus.Open.ToString()
            let remainQty = (tempStock.QtyRequest ?? 0m) - (tempStock.QtyUsed ?? 0m)
            where remainQty > 0m
            group remainQty by new
            {
                tempStock.Code,
                tempStock.VaCode,
                tempStock.CreatedDate
            }
            into tempStockGroup
            select new
            {
                Code = tempStockGroup.Key.Code,
                VaCode = tempStockGroup.Key.VaCode,
                ReservedKg = tempStockGroup.Sum(),
                tempStockGroup.Key.CreatedDate
            })
            .ToListAsync(cancellationToken);

        var reservedVaMap = reservedVaRows
            .GroupBy(x => NormalizeCode(x.Code))
            .ToDictionary(
                x => x.Key,
                x => x
                    .OrderBy(y => y.VaCode)
                    .ThenBy(y => y.CreatedDate)
                    .Select(y => new ReservedVaCodeDto
                    {
                        VaCode = y.VaCode,
                        ReservedKg = y.ReservedKg,
                        CreatedDate = y.CreatedDate
                    })
                    .ToList());

        var headerMap = items.ToDictionary(x => StockKey(x.Code, x.StockType));
        foreach (var detail in detailRows)
        {
            if (!headerMap.TryGetValue(StockKey(detail.Code, detail.StockType), out var header))
            {
                continue;
            }

            header.StockDetailAvailables.Add(new StockAvailableDetailDto
            {
                LotNo = detail.LotNo,
                ShelfStockCode = WarehouseStockQueryHelper.GetShelfDisplayName(detail.ShelfStockCode),
                CompanyName = detail.CompanyName,
                OnHandKg = detail.OnHandKg
            });
        }

        foreach (var item in items)
        {
            var isDefective = IsDefectiveStockType(item.StockType);
            var normalizedCode = NormalizeCode(item.Code);

            item.ReservedOpenAllKg = !isDefective && reservedMap.TryGetValue(normalizedCode, out var reserved)
                ? reserved
                : 0m;
            item.AvailableKg = isDefective ? 0m : item.TotalOnHandKg - item.ReservedOpenAllKg;

            if (!isDefective && reservedVaMap.TryGetValue(normalizedCode, out var reservedVaCodes))
            {
                item.ReservedVaCodes = reservedVaCodes;
            }

            if (IsRawMaterialStockType(item.StockType) && materialMap.TryGetValue(item.Code, out var material))
            {
                item.CodeName = material.Name;
                item.CategoryName = material.CategoryName;
            }
            else if (IsFinishedGoodStockType(item.StockType) && productMap.TryGetValue(item.Code, out var product))
            {
                item.CodeName = product.Name;
                item.CategoryName = product.CategoryName;
            }
        }

        if (request.OnlyAvailableLeZero)
        {
            items = items.Where(x => x.AvailableKg <= 0m).ToList();
        }

        if (request.AvailableMax.HasValue)
        {
            items = items.Where(x => x.AvailableKg <= request.AvailableMax.Value).ToList();
        }

        return items;
    }

    private static bool IsRawMaterialStockType(StockType stockType)
        => stockType is StockType.RawMaterial or StockType.DefectiveRawMaterial;

    private static bool IsFinishedGoodStockType(StockType stockType)
        => stockType is StockType.FinishedGood or StockType.DefectiveFinishedGood;

    private static bool IsDefectiveStockType(StockType stockType)
        => stockType is StockType.DefectiveRawMaterial or StockType.DefectiveFinishedGood;

    private static string NormalizeCode(string? code)
        => (code ?? string.Empty).Trim().ToUpperInvariant();

    private static string StockKey(string? code, StockType stockType)
        => $"{NormalizeCode(code)}|{(int)stockType}";

    private sealed record ItemNameProjection(string Code, string Name, string CategoryName);

    private sealed record StockDetailProjection(
        string? Code,
        StockType StockType,
        string ShelfStockCode,
        string CompanyName,
        string? LotNo,
        decimal OnHandKg);
}
