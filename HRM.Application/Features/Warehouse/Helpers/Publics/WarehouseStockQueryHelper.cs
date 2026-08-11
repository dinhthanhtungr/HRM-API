using HRM.Domain.Entities.WarehouseSchema;
using HRM.Domain.Enums.WareHouses;

namespace HRM.Application.Features.Warehouse.Helpers.Publics;

/// <summary>
/// Gom rule lọc tồn kho dùng chung để các màn hình preview/list không tự viết lệch nhau.
/// </summary>
public static class WarehouseStockQueryHelper
{
    public const string MixingShelfCode = "CT.0.1";
    public const string MixingShelfDisplayName = "CT.0.1 - KHO CÂN TRỘN";

    public static IQueryable<WarehouseShelfStock> ActiveShelfStocks(
        IQueryable<WarehouseShelfStock> query,
        Guid companyId)
    {
        return query.Where(stock =>
            stock.CompanyId == companyId &&
            stock.Code != null &&
            stock.Code != string.Empty &&
            stock.WarehouseShelves != null &&
            stock.WarehouseShelves.IsActive);
    }

    public static IQueryable<WarehouseShelfStock> ForItemStock(
        IQueryable<WarehouseShelfStock> query,
        Guid companyId,
        string code,
        StockType stockType,
        bool excludeMixingShelf = false)
    {
        var normalizedCode = code.Trim();
        var filteredQuery = ActiveShelfStocks(query, companyId)
            .Where(stock =>
                stock.Code == normalizedCode &&
                stock.StockType == stockType);

        return excludeMixingShelf
            ? filteredQuery.Where(stock => stock.ShelfStockCode != MixingShelfCode)
            : filteredQuery;
    }

    public static string GetShelfDisplayName(string shelfStockCode)
    {
        return shelfStockCode == MixingShelfCode
            ? MixingShelfDisplayName
            : shelfStockCode;
    }
}
