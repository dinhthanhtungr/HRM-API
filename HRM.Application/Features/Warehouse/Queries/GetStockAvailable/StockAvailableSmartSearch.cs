using HRM.Application.Abstractions.Persistence.Warehouse;
using HRM.Application.Commons.Searching;
using HRM.Domain.Entities.WarehouseSchema;
using HRM.Domain.Enums.WareHouses;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Warehouse.Queries.GetStockAvailable;

internal static class StockAvailableSmartSearch
{
    internal static IQueryable<WarehouseShelfStock> FilterAvailability(IWarehouseReadDbContext db,
        IQueryable<WarehouseShelfStock> scoped, Guid company, bool nonPositive, decimal? maximum)
    {
        var reserved = db.WarehouseTempStocks.AsNoTracking().Where(t => t.CompanyId == company && t.ReserveStatus == "Open" &&
            (t.QtyRequest ?? 0m) > (t.QtyUsed ?? 0m));
        var totals = scoped.GroupBy(s => new { s.Code, s.StockType }).Select(stocks => new {
            stocks.Key.Code, stocks.Key.StockType,
            Total = stocks.Sum(s => s.WarehouseShelves!.IsActive ? (decimal?)s.QtyKg : 0m) ?? 0m
        });
        var eligible = from stock in totals
            let remaining = reserved.Where(t => t.Code == stock.Code).Sum(t => (decimal?)((t.QtyRequest ?? 0m) - (t.QtyUsed ?? 0m))) ?? 0m
            let available = stock.StockType == StockType.DefectiveRawMaterial || stock.StockType == StockType.DefectiveFinishedGood
                ? 0m : stock.Total - remaining
            where (!nonPositive || available <= 0m) && (!maximum.HasValue || available <= maximum.Value)
            select new { stock.Code, stock.StockType };
        return scoped.Where(s => eligible.Any(e => e.Code == s.Code && e.StockType == s.StockType));
    }

    internal static SmartSearchSchema<WarehouseShelfStock> Build(IWarehouseReadDbContext db,
        IQueryable<WarehouseShelfStock> visible, Guid companyId) => SmartSearch.For(visible, fields => [
        fields.Codes("materialCode", stock => db.Materials.Where(m => (stock.StockType == StockType.RawMaterial || stock.StockType == StockType.DefectiveRawMaterial) && m.CompanyId == companyId && m.ExternalId == stock.Code),
            m => m.ExternalId, "Mã NVL", "Material code"),
        fields.Names("materialName", stock => db.Materials.Where(m => (stock.StockType == StockType.RawMaterial || stock.StockType == StockType.DefectiveRawMaterial) && m.CompanyId == companyId && m.ExternalId == stock.Code),
            m => m.Name, "Tên NVL", "Material name"),
        fields.Codes("productCode", stock => db.Products.Where(p => (stock.StockType == StockType.FinishedGood || stock.StockType == StockType.DefectiveFinishedGood) && p.CompanyId == companyId && p.ColourCode == stock.Code),
            p => p.Code, "Mã sản phẩm", "Product code"),
        fields.Names("productName", stock => db.Products.Where(p => (stock.StockType == StockType.FinishedGood || stock.StockType == StockType.DefectiveFinishedGood) && p.CompanyId == companyId && p.ColourCode == stock.Code),
            p => p.Name, "Tên sản phẩm", "Product name"),
        fields.Codes("colourCode", stock => db.Products.Where(p => (stock.StockType == StockType.FinishedGood || stock.StockType == StockType.DefectiveFinishedGood) && p.CompanyId == companyId && p.ColourCode == stock.Code),
            p => p.ColourCode, "ColorCode", "Mã màu"),
        fields.Code("batchNumber", stock => stock.LotNo ?? stock.LotKey, "Số lô", "Batch")
    ]);
}
