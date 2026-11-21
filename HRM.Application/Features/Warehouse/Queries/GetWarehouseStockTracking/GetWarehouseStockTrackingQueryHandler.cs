using HRM.Application.Abstractions.Persistence.Warehouse;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.Warehouse.Helpers.Publics;
using HRM.Domain.Enums.WareHouses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Warehouse.Queries.GetWarehouseStockTracking;

internal sealed class GetWarehouseStockTrackingQueryHandler(
    IWarehouseReadDbContext db, ICurrentUser user, ICurrentUserPermissionService permissions)
    : IRequestHandler<GetWarehouseStockTrackingQuery, PagedResult<WarehouseStockTrackingDto>?>
{
    private const int PageSize = 25;

    public async Task<PagedResult<WarehouseStockTrackingDto>?> Handle(
        GetWarehouseStockTrackingQuery request, CancellationToken cancellationToken)
    {
        if (!permissions.HasPermission(ApplicationPermissions.Warehouse.ViewStockTracking) ||
            user.CompanyId is not { } companyId || companyId == Guid.Empty ||
            user.EmployeeId.GetValueOrDefault() == Guid.Empty)
            return null;

        var query = BuildRows(db, companyId);
        var keyword = request.Keyword?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(keyword))
            query = query.Where(row => row.Code.ToLower().Contains(keyword) ||
                (row.Name != null && row.Name.ToLower().Contains(keyword)));

        var count = await query.CountAsync(cancellationToken);
        var page = Math.Clamp(request.PageNumber, 1, Math.Max(1, (int)Math.Ceiling(count / (double)PageSize)));
        var items = await query.OrderBy(row => row.Code).ThenBy(row => row.StockType)
            .Skip((page - 1) * PageSize).Take(PageSize).ToListAsync(cancellationToken);
        return new(items, count, page, PageSize);
    }

    internal static IQueryable<WarehouseStockTrackingDto> BuildRows(IWarehouseReadDbContext db, Guid companyId)
    {
        var shelfRows = WarehouseStockQueryHelper.ActiveShelfStocks(db.WarehouseShelfStocks.AsNoTracking(), companyId)
            .Where(stock => stock.Code != null && stock.Code != "" &&
                stock.WarehouseShelves!.CompanyId == companyId &&
                stock.ShelfStockCode != WarehouseStockQueryHelper.MixingShelfCode &&
                stock.WarehouseShelves.SlotCode != WarehouseStockQueryHelper.MixingShelfCode &&
                (stock.StockType == StockType.RawMaterial || stock.StockType == StockType.FinishedGood))
            .GroupBy(stock => new { stock.Code, stock.StockType })
            .Select(group => new
            {
                Code = group.Key.Code!,
                group.Key.StockType,
                OnHandKg = group.Sum(stock => (decimal?)stock.QtyKg) ?? 0m,
            });

        return from stock in shelfRows
               select new WarehouseStockTrackingDto
               {
                   Code = stock.Code,
                   Name = stock.StockType == StockType.RawMaterial
                       ? db.Materials.Where(material => material.CompanyId == companyId && material.ExternalId == stock.Code)
                           .Select(material => material.Name).OrderBy(name => name).FirstOrDefault()
                       : db.Products.Where(product => product.CompanyId == companyId && product.ColourCode == stock.Code)
                           .Select(product => product.Name).OrderBy(name => name).FirstOrDefault(),
                   StockType = stock.StockType,
                   OnHandKg = stock.OnHandKg,
                   AvailableKg = stock.StockType == StockType.FinishedGood &&
                       !db.Materials.Any(material => material.CompanyId == companyId && material.ExternalId == stock.Code)
                       ? stock.OnHandKg - (db.WarehouseTempStocks
                           .Where(reserve => reserve.CompanyId == companyId && reserve.Code == stock.Code &&
                               reserve.ReserveStatus == ReserveStatus.Open.ToString() &&
                               (reserve.QtyRequest ?? 0m) > (reserve.QtyUsed ?? 0m))
                           .Sum(reserve => (decimal?)((reserve.QtyRequest ?? 0m) - (reserve.QtyUsed ?? 0m))) ?? 0m)
                       : null,
               };
    }
}
