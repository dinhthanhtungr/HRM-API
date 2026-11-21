using HRM.Application.Abstractions.Persistence.Warehouse;
using HRM.Application.Features.Warehouse.Dtos;
using HRM.Domain.Enums.WareHouses;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Warehouse.Services;

internal static class WarehouseDefectReasonReader
{
    internal sealed class Row
    {
        public long VoucherDetailId { get; init; }
        public string ProductCode { get; init; } = string.Empty;
        public string LotNumber { get; init; } = string.Empty;
        public string? Note { get; init; }
        public string RequestCode { get; init; } = string.Empty;
        public string VoucherCode { get; init; } = string.Empty;
        public DateTime? RecordedAt { get; init; }
    }

    internal static string? ParseReason(string? note)
    {
        if (note == null) return null;
        foreach (var line in note.Split('\n'))
        {
            if (!line.TrimStart().StartsWith("[WIP_YIELD_CHECK]", StringComparison.Ordinal)) continue;
            const string marker = "; REASON=";
            var index = line.IndexOf(marker, StringComparison.Ordinal);
            if (index < 0) continue;
            var reason = line[(index + marker.Length)..].Trim();
            if (reason.Length > 0) return reason;
        }
        return null;
    }

    internal static IQueryable<Row> Query(IWarehouseReadDbContext db, Guid company)
        => from receipt in db.ProductionOutputReceiptLedgers.AsNoTracking()
           join flow in db.ImportRequestFlowLogs.AsNoTracking()
               on new { receipt.CompanyId, receipt.RequestCode, receipt.RequestDetailId, receipt.VoucherId }
               equals new { flow.CompanyId, RequestCode = (string?)flow.RequestCode, flow.RequestDetailId, VoucherId = flow.AppliedVoucherId }
           join voucher in db.WarehouseVouchers.AsNoTracking() on receipt.VoucherId equals (long?)voucher.VoucherId
           join detail in db.WarehouseVoucherDetails.AsNoTracking() on receipt.VoucherDetailId equals (long?)detail.VoucherDetailId
           join request in db.WarehouseRequests.AsNoTracking() on voucher.RequestId equals (int?)request.RequestId
           where receipt.CompanyId == company && flow.CompanyId == company && voucher.CompanyId == company && request.CompanyId == company
               && receipt.RequestDetailId != null && receipt.VoucherDetailId != null && receipt.VoucherId != null
               && detail.VoucherId == voucher.VoucherId && flow.RequestId == request.RequestId
               && request.ReqType == WareHouseRequestType.ImportDefectiveFinishedGood
               && request.RequestCode == flow.RequestCode
               && receipt.ItemStockType == (int)StockType.DefectiveFinishedGood && flow.StockType == receipt.ItemStockType
               && receipt.QtyKg > 0 && flow.AppliedQtyKg > 0 && detail.IsIncrease
               && flow.LogStatus == "APPLIED" && flow.CancelledAt == null
               && receipt.ProductCode != null && receipt.LotNumber != null && receipt.LotNumber!.Trim() != ""
               && receipt.ProductCode!.Trim().ToUpper() == detail.ProductCode.Trim().ToUpper()
               && detail.LotNumber != null && receipt.LotNumber!.Trim().ToUpper() == detail.LotNumber!.Trim().ToUpper()
               && flow.ProductCode.Trim().ToUpper() == detail.ProductCode.Trim().ToUpper()
               && flow.LotNumber != null && flow.LotNumber!.Trim().ToUpper() == detail.LotNumber!.Trim().ToUpper()
               && flow.Note != null && flow.Note!.Contains("[WIP_YIELD_CHECK]")
           select new Row { VoucherDetailId = detail.VoucherDetailId, ProductCode = detail.ProductCode, LotNumber = detail.LotNumber!, Note = flow.Note,
               RequestCode = request.RequestCode, VoucherCode = voucher.VoucherCode, RecordedAt = flow.ConfirmedAt ?? flow.AppliedAt };

    private static DefectReasonDto[] Reasons(IEnumerable<Row> rows) => rows
        .OrderByDescending(x => x.RecordedAt).ThenBy(x => x.VoucherCode, StringComparer.Ordinal)
        .Select(x => new { Row = x, Reason = ParseReason(x.Note) }).Where(x => x.Reason != null)
        .Select(x => new DefectReasonDto(x.Reason!, "ImportConfirmation", x.Row.RequestCode, x.Row.VoucherCode, x.Row.RecordedAt))
        .Distinct().ToArray();

    public static async Task EnrichVouchersAsync(IWarehouseReadDbContext db, Guid company,
        IReadOnlyList<WarehouseVoucherDetailDto> details, CancellationToken ct)
    {
        if (details.Count == 0) return;
        var ids = details.Select(x => x.VoucherDetailId).Distinct().ToArray();
        var rows = await Query(db, company).Where(x => ids.Contains(x.VoucherDetailId)).ToArrayAsync(ct);
        var map = rows.GroupBy(x => x.VoucherDetailId).ToDictionary(g => g.Key, Reasons);
        foreach (var detail in details) detail.DefectReasons = map.GetValueOrDefault(detail.VoucherDetailId) ?? [];
    }

    private static (string Code, string Lot) Key(string code, string lot) => (code.Trim().ToUpperInvariant(), lot.Trim().ToUpperInvariant());

    public static async Task EnrichStockAsync(IWarehouseReadDbContext db, Guid company,
        IReadOnlyList<StockAvailableDto> items, CancellationToken ct)
    {
        var eligible = items.Where(x => x.StockType == StockType.DefectiveFinishedGood).ToArray();
        var codes = eligible.Select(x => Key(x.Code, "").Code).Distinct().ToArray();
        var lots = eligible.SelectMany(x => x.StockDetailAvailables).Select(x => x.LotNo)
            .OfType<string>().Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => Key("", x).Lot).Distinct().ToArray();
        if (codes.Length == 0 || lots.Length == 0) return;
        var rows = await Query(db, company).Where(x => codes.Contains(x.ProductCode.Trim().ToUpper()) && lots.Contains(x.LotNumber.Trim().ToUpper())).ToArrayAsync(ct);
        var map = rows.GroupBy(x => Key(x.ProductCode, x.LotNumber)).ToDictionary(g => g.Key, Reasons);
        foreach (var item in eligible)
            foreach (var detail in item.StockDetailAvailables)
                if (!string.IsNullOrWhiteSpace(detail.LotNo))
                    detail.DefectReasons = map.GetValueOrDefault(Key(item.Code, detail.LotNo)) ?? [];
    }
}
