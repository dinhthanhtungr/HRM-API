using System.Globalization;
using System.Text.Json;
using HRM.Application.Abstractions.Persistence.Timeline;
using HRM.Application.Features.Timeline.Dtos;
using HRM.Domain.Enums.Logs;
using HRM.Domain.Enums.WareHouses;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Timeline.Queries.GetSaleOrderTimelineDetail;

internal static class SaleOrderWarehouseReceiptTimelineQuery
{
    internal const string ImportedStatus = "WarehouseImported";

    // Chỉ quy thuộc lot lịch sử khi công thức xác định duy nhất một MFG trong company.
    internal static async Task<List<TimelineItemDto>> LoadAsync(
        ITimelineDbContext dbContext,
        Guid companyId,
        Guid[] mfgIds,
        string? status,
        CancellationToken cancellationToken)
    {
        if (mfgIds.Length == 0 ||
            (!string.IsNullOrWhiteSpace(status) && status.Trim() != ImportedStatus))
        {
            return [];
        }

        var pageSources = await dbContext.MfgProductionOrders
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.IsActive && mfgIds.Contains(x.MfgProductionOrderId))
            .SelectMany(x => x.ProductionSelectVersions.Where(v =>
                v.CompanyId == companyId && v.ValidFrom.HasValue &&
                v.ManufacturingFormula != null && v.ManufacturingFormula.CompanyId == companyId &&
                v.ManufacturingFormula.ExternalId != string.Empty), (mfg, version) => new
            {
                MfgId = mfg.MfgProductionOrderId,
                MfgCode = mfg.ExternalId,
                LotNumber = version.ManufacturingFormula!.ExternalId
            })
            .Distinct()
            .ToListAsync(cancellationToken);
        var lots = pageSources.Select(x => x.LotNumber).Distinct().ToArray();
        if (lots.Length == 0)
        {
            return [];
        }

        // Bao gồm cả MFG inactive trong kiểm tra mơ hồ để không gán lịch sử của đơn cũ cho đơn mới.
        var owners = await dbContext.MfgProductionOrders.AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .SelectMany(x => x.ProductionSelectVersions.Where(v =>
                v.CompanyId == companyId && v.ValidFrom.HasValue &&
                v.ManufacturingFormula != null && v.ManufacturingFormula.CompanyId == companyId &&
                lots.Contains(v.ManufacturingFormula.ExternalId)), (mfg, version) => new
            {
                MfgId = mfg.MfgProductionOrderId,
                LotNumber = version.ManufacturingFormula!.ExternalId
            })
            .Distinct()
            .ToListAsync(cancellationToken);
        var uniqueLots = owners.GroupBy(x => x.LotNumber)
            .Where(x => x.Select(v => v.MfgId).Distinct().Count() == 1)
            .Select(x => x.Key).ToArray();
        var sources = pageSources.Where(x => uniqueLots.Contains(x.LotNumber))
            .GroupBy(x => x.LotNumber).ToDictionary(x => x.Key, x => x.First());
        if (sources.Count == 0)
        {
            return [];
        }

        var receipts = await (
            from ledger in dbContext.WarehouseShelfLedgers.AsNoTracking()
            join detail in dbContext.WarehouseVoucherDetails.AsNoTracking()
                on ledger.VoucherDetailId equals (long?)detail.VoucherDetailId
            where ledger.CompanyId == companyId && detail.Voucher.CompanyId == companyId &&
                ledger.VoucherId == detail.VoucherId &&
                ledger.StockType == StockType.FinishedGood && ledger.DeltaKg > 0 &&
                detail.VoucherType == VoucherDetailType.Import && detail.IsIncrease && detail.IsApplied &&
                ledger.LotNumber != null && uniqueLots.Contains(ledger.LotNumber)
            orderby ledger.CreatedAt, ledger.LedgerId
            select new
            {
                ledger.LedgerId,
                ledger.VoucherId,
                ledger.VoucherDetailId,
                ledger.RequestCode,
                ledger.LotNumber,
                QtyKg = ledger.DeltaKg,
                ledger.UnitName,
                ledger.CreatedAt,
                ledger.CreatedBy
            })
            .ToListAsync(cancellationToken);

        return receipts.Select(x => new TimelineItemDto
        {
            SourceType = "WarehouseShelfLedger",
            SourceId = sources[x.LotNumber!].MfgId,
            SourceCode = sources[x.LotNumber!].MfgCode,
            EventType = EventType.WarehouseReceipt,
            Status = ImportedStatus,
            Note = $"Nhập kho: {x.QtyKg.ToString("0.###", CultureInfo.InvariantCulture)} {x.UnitName}",
            CreatedDate = x.CreatedAt,
            CreatedBy = x.CreatedBy ?? Guid.Empty,
            CompanyId = companyId,
            PayloadJson = JsonSerializer.Serialize(new
            {
                ledgerId = x.LedgerId,
                voucherId = x.VoucherId,
                voucherDetailId = x.VoucherDetailId,
                requestCode = x.RequestCode,
                lotNumber = x.LotNumber,
                quantity = x.QtyKg,
                unitName = x.UnitName
            })
        }).ToList();
    }
}
