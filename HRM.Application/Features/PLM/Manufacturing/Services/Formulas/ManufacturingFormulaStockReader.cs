using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;
using HRM.Application.Features.PLM.ProductionOrders.Dtos;
using HRM.Domain.Enums.Formulas;
using HRM.Domain.Enums.WareHouses;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Services.Formulas;

internal static class ManufacturingFormulaStockReader
{
    // Chỉ lấy lot đã lưu từ VA được handler xác minh là nguồn/lựa chọn hợp lệ.
    public static async Task<List<ProductionOrderFormulaSourceItemDto>> FillAsync(
        IManufacturingDbContext db,
        Guid company,
        IReadOnlyList<ProductionOrderFormulaSourceItemDto> items,
        CancellationToken ct,
        bool canViewDefectiveLots = false,
        bool useGoodFifoDefault = false)
    {
        var productIds = items.Where(x => !Material(x.ItemType)).Select(x => x.ItemId).Distinct().ToArray();
        var products = await db.Products.AsNoTracking()
            .Where(x => x.CompanyId == company && productIds.Contains(x.ProductId))
            .ToDictionaryAsync(x => x.ProductId, x => x.ColourCode ?? x.Code, ct);
        string? Code(ProductionOrderFormulaSourceItemDto x) => Material(x.ItemType) ? x.Code : products.GetValueOrDefault(x.ItemId) ?? x.Code;
        static string Key(string? code) => code?.Trim().ToUpperInvariant() ?? string.Empty;
        var codes = items.Select(x => Key(Code(x))).Where(x => x.Length > 0).Distinct().ToArray();
        var stock = await db.WarehouseShelfStocks.AsNoTracking()
            .Where(x => x.CompanyId == company && codes.Contains(x.Code.Trim().ToUpper())
                && x.WarehouseShelves != null && x.WarehouseShelves.CompanyId == company && x.WarehouseShelves.IsActive
                && x.ShelfStockCode != HRM.Application.Features.Warehouse.Helpers.Publics.WarehouseStockQueryHelper.MixingShelfCode
                && x.WarehouseShelves.SlotCode != HRM.Application.Features.Warehouse.Helpers.Publics.WarehouseStockQueryHelper.MixingShelfCode)
            .Select(x => new { x.Code, x.LotNo, x.StockType, x.QtyKg, x.Bags, x.UpdatedDate, x.ShelfStockId, x.WarehouseShelves!.SlotCode })
            .ToListAsync(ct);
        var reserves = await db.WarehouseTempStocks.AsNoTracking()
            .Where(x => x.CompanyId == company && codes.Contains(x.Code.Trim().ToUpper()) && x.ReserveStatus == "Open")
            .Select(x => new { x.Code, x.QtyRequest }).ToListAsync(ct);
        var onHand = stock.GroupBy(x => Key(x.Code)).ToDictionary(x => x.Key, x => x.Sum(s => s.QtyKg));
        var reserved = reserves.GroupBy(x => Key(x.Code)).ToDictionary(x => x.Key, x => x.Sum(s => s.QtyRequest ?? 0));
        var lotRows = stock.Where(x => x.SlotCode != "CT.0.1" && !string.IsNullOrWhiteSpace(x.LotNo)
                && (x.StockType is StockType.RawMaterial or StockType.FinishedGood
                    || canViewDefectiveLots && Defective(x.StockType)))
            .GroupBy(x => new { Code = Key(x.Code), Lot = x.LotNo!.Trim(), x.StockType })
            .Where(g => g.Sum(x => x.QtyKg) > 0)
            .Select(g => new { g.Key.Code, Lot = Option(g.Key.Lot, g.Key.StockType, g.Sum(x => x.QtyKg), g.Sum(x => x.Bags ?? 0)),
                FirstDate = g.Min(x => x.UpdatedDate), FirstId = g.Min(x => x.ShelfStockId) })
            .OrderBy(x => x.Lot.IsDefective).ThenBy(x => x.FirstDate).ThenBy(x => x.FirstId).ToList();
        return items.Select(item =>
        {
            var code = Code(item);
            var key = Key(code);
            var goodType = Material(item.ItemType) ? StockType.RawMaterial : StockType.FinishedGood;
            var badType = Material(item.ItemType) ? StockType.DefectiveRawMaterial : StockType.DefectiveFinishedGood;
            var lots = lotRows.Where(x => x.Code == key && (x.Lot.StockType == goodType || x.Lot.StockType == badType))
                .Select(x => x.Lot).ToList();
            if (!lots.Any(x => !x.IsDefective))
                lots.Insert(0, Option("N/A", goodType));

            if (useGoodFifoDefault)
            {
                // Lệnh New chỉ mượn thành phần nguồn; lot mặc định lấy từ tồn đạt
                // hiện tại theo FIFO, không mang lot/loại lỗi của VA tham khảo sang.
                var initial = lots.First(x => x.StockType == goodType);
                var quantity = onHand.GetValueOrDefault(key);
                var reserve = reserved.GetValueOrDefault(key);
                return item with
                {
                    ItemType = Material(item.ItemType) ? ItemType.Material : ItemType.Product,
                    LotNo = initial.LotNo, StockCode = code, OnHandKg = quantity,
                    ReservedOpenAllKg = reserve, AvailableKg = quantity - reserve, Lots = lots
                };
            }

            var savedType = item.ItemType is ItemType.MaterialFailure or ItemType.ProductFailure ? badType : goodType;
            var saved = !canViewDefectiveLots && Defective(savedType) ? null : item.LotNo?.Trim();
            if (!string.IsNullOrWhiteSpace(saved) && !lots.Any(x => x.StockType == savedType
                    && string.Equals(x.LotNo, saved, StringComparison.OrdinalIgnoreCase)))
            {
                // Giữ lot nguồn đã hết tồn nhưng không trình bày như tồn đạt/lỗi hiện có.
                var persisted = Option(saved, savedType);
                persisted.QualityStatus = "Unknown";
                persisted.QualityStatusName = "Không có trong kho hiện tại";
                lots.Add(persisted);
            }
            lots = lots.OrderBy(x => x.IsDefective).ToList();
            var on = onHand.GetValueOrDefault(key);
            var held = reserved.GetValueOrDefault(key);
            return item with { LotNo = saved, StockCode = code, OnHandKg = on, ReservedOpenAllKg = held,
                AvailableKg = on - held, Lots = lots };
        }).ToList();
    }

    private static bool Material(ItemType type) => type is ItemType.Material or ItemType.MaterialFailure;
    private static bool Defective(StockType type) => type is StockType.DefectiveFinishedGood or StockType.DefectiveRawMaterial;
    private static ProductionOrderLotNumberDto Option(string lot, StockType type, decimal kg = 0, int bags = 0) => new()
    {
        LotNo = lot, StockType = type, IsDefective = Defective(type),
        QualityStatus = Defective(type) ? "Fail" : "Pass",
        QualityStatusName = Defective(type) ? "Lỗi" : "Đạt", QuantityKg = kg, Bags = bags
    };
}
