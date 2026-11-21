using HRM.Application.Abstractions.Persistence.Warehouse;
using HRM.Application.Features.Warehouse.Dtos;
using HRM.Domain.Entities.WarehouseSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Warehouse.Queries.GetStockJourney;

internal static class StockJourneyCurrentReader
{
    public static async Task<IReadOnlyList<StockJourneyLocationDto>> LocationsAsync(
        IWarehouseReadDbContext db, Guid company, string code, string lot, bool canViewMixing, CancellationToken ct)
        => await LocationsQuery(db, company, code, lot, canViewMixing).ToArrayAsync(ct);

    internal static IQueryable<StockJourneyLocationDto> LocationsQuery(
        IWarehouseReadDbContext db, Guid company, string code, string lot, bool canViewMixing)
    {
        var stocks = db.WarehouseShelfStocks.AsNoTracking().Where(x => x.CompanyId == company &&
            x.Code.Trim().ToUpper() == code && x.LotNo != null && x.LotNo.Trim().ToUpper() == lot && x.QtyKg > 0 &&
            x.WarehouseShelves != null && x.WarehouseShelves.CompanyId == company);
        if (!canViewMixing)
            stocks = stocks.Where(x => !x.WarehouseShelves!.SlotCode.ToUpper().StartsWith("CT.") &&
                !x.ShelfStockCode.ToUpper().StartsWith("CT."));
        return stocks.GroupBy(x => new { x.WarehouseShelves!.SlotCode, x.StockType, x.WarehouseShelves.IsActive })
            .OrderBy(g => g.Key.SlotCode).ThenBy(g => g.Key.StockType)
            .Select(g => new StockJourneyLocationDto(g.Key.SlotCode, (int)g.Key.StockType, g.Sum(x => x.QtyKg), g.Key.IsActive));
    }

    public static async Task<IReadOnlyList<StockJourneyUsageDto>> AllocationsAsync(
        IWarehouseReadDbContext db, IQueryable<WarehouseMovementRow> lotRows, Guid company, string code, string lot, CancellationToken ct)
        => await AllocationsQuery(db, lotRows, company, code, lot).ToArrayAsync(ct);

    internal static IQueryable<StockJourneyUsageDto> AllocationsQuery(
        IWarehouseReadDbContext db, IQueryable<WarehouseMovementRow> lotRows, Guid company, string code, string lot)
    {
        // Only an actual outgoing ledger-to-buffer document link establishes this destination.
        var links = lotRows.Where(x => x.DeltaKg < 0 && x.VoucherDetailId.HasValue)
            .Select(x => new { x.VoucherDetailId, x.VoucherId }).Distinct();
        var query = from buffer in db.OperationMaterialBuffers.AsNoTracking()
                    join link in links on buffer.VoucherDetailId equals link.VoucherDetailId
                    where buffer.CompanyId == company && buffer.ProductCode.Trim().ToUpper() == code &&
                        buffer.LotNumber != null && buffer.LotNumber.Trim().ToUpper() == lot &&
                        (!buffer.VoucherId.HasValue || !link.VoucherId.HasValue || buffer.VoucherId == link.VoucherId)
                    select new { buffer.BufferId, buffer.VaExternalId, buffer.MfgExternalId, buffer.OperationName,
                        buffer.IssuedQtyKg, buffer.UsedQtyKg, buffer.ReturnedQtyKg, buffer.RemainingQtyKg };
        return query.Distinct().OrderBy(x => x.BufferId).Select(x => new StockJourneyUsageDto(x.BufferId,
            x.VaExternalId, x.MfgExternalId, x.OperationName, x.IssuedQtyKg, x.UsedQtyKg, x.ReturnedQtyKg, x.RemainingQtyKg));
    }
}
