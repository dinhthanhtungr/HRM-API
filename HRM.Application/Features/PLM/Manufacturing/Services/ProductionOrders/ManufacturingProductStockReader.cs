using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;
using HRM.Domain.Enums.WareHouses;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Services.ProductionOrders;

internal static class ManufacturingProductStockReader
{
    public static async Task<IReadOnlyList<ManufacturingProductLotDto>> ReadAsync(
        IManufacturingDbContext db,
        Guid company,
        Guid productId,
        CancellationToken ct
    )
    {
        var code = await db.Products.AsNoTracking().Where(x => x.ProductId == productId && x.CompanyId == company && x.IsActive)
            .Select(x => x.ColourCode)
            .FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(code))
        {
            return[];
        }

        var rows = await db.WarehouseShelfStocks.AsNoTracking().Where(x => x.CompanyId == company && x.Code == code.Trim() && x.QtyKg > 0 && x.LotNo != null && x.LotNo != ""
            && x.WarehouseShelves != null
            && x.WarehouseShelves.CompanyId == company
            && x.WarehouseShelves.IsActive
            && x.WarehouseShelves.SlotCode != "CT.0.1")
            .Select(x => new { x.Code, x.LotNo, x.StockType, x.QtyKg, x.Bags })
            .ToListAsync(ct);
        var lots = rows.Select(x => x.LotNo!.Trim()).Distinct().ToArray();
        var dates = await (
            from f in db.ManufacturingFormulas.AsNoTracking()join v in db.ProductionSelectVersions.AsNoTracking()on f.ManufacturingFormulaId equals v.ManufacturingFormulaId
            join o in db.MfgProductionOrders.AsNoTracking()on v.MfgProductionOrderId equals o.MfgProductionOrderId
            where f.CompanyId == company && v.CompanyId == company && o.CompanyId == company
            && lots.Contains(f.ExternalId)group o.ManufacturingDate ?? o.CreatedDate by f.ExternalId into g
                select new
                {
                    Code = g.Key,
                    Date = g.Max()
                }

        ).ToDictionaryAsync(x => x.Code, x => (DateTime? )x.Date, ct);
        return rows.GroupBy(x => new { x.Code, Lot = x.LotNo!.Trim(), x.StockType }).Select(x => new ManufacturingProductLotDto(
            x.Key.Code,
            x.Key.Lot,
            x.Sum(s => s.QtyKg),
            x.Sum(s => s.Bags ?? 0),
            dates.GetValueOrDefault(x.Key.Lot),
                x.Key.StockType switch
        {
            StockType.FinishedGood or StockType.RawMaterial => "Pass",
            StockType.DefectiveFinishedGood or StockType.DefectiveRawMaterial => "Fail",
            StockType.Waiter => "PendingQC",
            _ => x.Key.StockType.ToString()}
        ))
            .OrderBy(x => x.ManufacturingDate ?? DateTime.MaxValue)
            .ThenBy(x => x.LotNo)
            .ToList();
    }
}
