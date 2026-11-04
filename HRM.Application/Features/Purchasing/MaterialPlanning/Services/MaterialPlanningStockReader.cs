using HRM.Application.Abstractions.Persistence.Warehouse;
using HRM.Application.Features.Warehouse.Helpers.Publics;
using HRM.Domain.Enums.WareHouses;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Purchasing.MaterialPlanning.Services;

internal static class MaterialPlanningStockReader
{
    public static async Task<Dictionary<(string Code, StockType Type), decimal>> ReadAsync(
        IWarehouseReadDbContext db, Guid company, string[] codes, CancellationToken ct)
    {
        var keys = codes.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim().ToUpperInvariant()).Distinct().ToArray();
        if (keys.Length == 0) return [];
        var rows = await WarehouseStockQueryHelper.ActiveShelfStocks(db.WarehouseShelfStocks.AsNoTracking(), company)
            .Where(x => x.WarehouseShelves!.CompanyId == company && keys.Contains(x.Code.Trim().ToUpper()) &&
                (x.StockType == StockType.RawMaterial || x.StockType == StockType.FinishedGood))
            .GroupBy(x => new { Code = x.Code.Trim().ToUpper(), x.StockType })
            .Select(g => new { g.Key.Code, g.Key.StockType, Quantity = g.Sum(x => x.QtyKg) }).ToListAsync(ct);
        return rows.ToDictionary(x => (x.Code, x.StockType), x => x.Quantity);
    }

    public static decimal Get(Dictionary<(string Code, StockType Type), decimal> stock, string? code, bool material)
        => stock.GetValueOrDefault(((code ?? "").Trim().ToUpperInvariant(), material ? StockType.RawMaterial : StockType.FinishedGood));
}
