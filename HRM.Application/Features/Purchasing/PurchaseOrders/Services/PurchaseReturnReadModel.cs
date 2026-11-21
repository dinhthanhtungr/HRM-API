using HRM.Application.Abstractions.Persistence.Purchasing;
using HRM.Application.Features.Purchasing.PurchaseOrders.Dtos;
using HRM.Domain.Enums.Devandqa;
using HRM.Domain.Enums.WareHouses;
using HRM.Domain.Enums.Orders;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Purchasing.PurchaseOrders.Services;

internal sealed record ReturnSource(long Id, string Code, string Name, string? Lot, decimal Kg, StockType Type);
internal sealed record ReturnUsage(string Code, string? Lot, StockType? Type, decimal Kg);
internal sealed class PurchaseReturnFacts
{
    public List<ReturnSource> Sources { get; } = [];
    public List<ReturnUsage> Committed { get; } = [];
    public List<PurchaseReturnRequestDto> Requests { get; } = [];
    public PurchaseReturnProgressDto Progress => new(Sources.Sum(x => x.Kg), Requests.Where(x => x.IsActive && x.Status != (int)WarehouseRequestStatus.Cancelled).Sum(x => x.RequestedQuantityKg),
        Requests.Sum(x => x.PostedQuantityKg), Requests.Sum(x => x.OutstandingQuantityKg),
        Sources.GroupBy(x => PurchaseReturnReadModel.Key(x.Code, x.Lot)).Sum(g => Math.Max(0,
            g.Sum(x => x.Kg) - Committed.Where(x => PurchaseReturnReadModel.Key(x.Code, x.Lot) == g.Key).Sum(x => x.Kg))));
}

/// <summary>Đọc theo lô cho danh sách và dialog. Xuất thực tế chỉ dựa ledger ròng của phiếu ExportReturn.</summary>
internal sealed class PurchaseReturnReadModel(IPurchaseOrderDbContext db)
{
    internal const string DocumentName = "Nghiệp vụ trả hàng NCC";
    private static readonly WareHouseRequestType[] Imports = [WareHouseRequestType.ImportRawMaterial,
        WareHouseRequestType.ImportOther, WareHouseRequestType.ImportMaterial, WareHouseRequestType.ImportDefectiveRawMaterial];

    public async Task<Dictionary<string, PurchaseReturnFacts>> ReadAsync(Guid company, IReadOnlyCollection<string> orderCodes, CancellationToken ct)
    {
        var codes = orderCodes.Select(Normalize).Where(x => x.Length > 0).Distinct().ToArray();
        if (codes.Length == 0) return [];
        var orders = await db.PurchaseOrders.AsNoTracking().Where(x => x.CompanyId == company && x.ExternalId != null &&
            codes.Contains(x.ExternalId.Trim().ToUpper())).Select(x => new { x.PurchaseOrderId, Code = x.ExternalId!.Trim().ToUpper() }).ToListAsync(ct);
        // String source cannot distinguish duplicate PO codes, including inactive historical duplicates.
        var unique = orders.GroupBy(x => x.Code).Where(x => x.Count() == 1).Select(x => x.Single()).ToList();
        var facts = unique.ToDictionary(x => x.Code, _ => new PurchaseReturnFacts());
        var validCodes = facts.Keys.ToArray();
        if (validCodes.Length == 0) return facts;
        var orderIds = unique.Select(x => x.PurchaseOrderId).ToArray();
        var materialRows = await db.PurchaseOrderDetails.AsNoTracking().Where(x => orderIds.Contains(x.PurchaseOrderId) && x.IsActive &&
            x.Material.CompanyId == company).Select(x => new { x.PurchaseOrderId, Code = x.MaterialExternalIDSnapshot ?? x.Material.ExternalId ?? "" }).ToListAsync(ct);
        var allowed = unique.ToDictionary(x => x.Code, x => materialRows.Where(m => m.PurchaseOrderId == x.PurchaseOrderId).Select(m => Normalize(m.Code)).ToHashSet());
        var requests = db.WarehouseRequests.AsNoTracking().Where(x => x.CompanyId == company && x.codeFromRequest != null &&
            validCodes.Contains(x.codeFromRequest.Trim().ToUpper()));
        var importTypes = Imports.Select(x => (int)x).ToArray();
        var receipts = await (from r in requests
                              join v in db.WarehouseVouchers.AsNoTracking() on (int?)r.RequestId equals v.RequestId
                              join d in db.WarehouseVoucherDetails.AsNoTracking() on v.VoucherId equals d.VoucherId
                              where r.IsActive && r.ReqStatus != WarehouseRequestStatus.Cancelled && Imports.Contains(r.ReqType) &&
                                  v.CompanyId == company && importTypes.Contains(v.VoucherType) && d.IsIncrease && d.QtyKg > 0
                              select new { OrderCode = r.codeFromRequest.Trim().ToUpper(), d.VoucherDetailId, d.ProductCode, d.ProductName, d.LotNumber, d.QtyKg }).ToListAsync(ct);
        var detailIds = receipts.Select(x => x.VoucherDetailId).ToArray();
        var inspections = await db.QCInputByQCs.AsNoTracking().Where(x => detailIds.Contains(x.VoucherDetailId))
            .Select(x => new { x.VoucherDetailId, x.QCInputByQCId, x.CreatedDate, x.ImportWarehouseType }).ToListAsync(ct);
        var latest = inspections.GroupBy(x => x.VoucherDetailId).ToDictionary(g => g.Key,
            g => g.OrderByDescending(x => x.CreatedDate).ThenByDescending(x => x.QCInputByQCId).First().ImportWarehouseType);
        foreach (var row in receipts.Where(x => allowed[x.OrderCode].Contains(Normalize(x.ProductCode))))
            if (latest.TryGetValue(row.VoucherDetailId, out var qc) && qc is QcDecision.QCFail or QcDecision.QCPass or QcDecision.Special)
                facts[row.OrderCode].Sources.Add(new(row.VoucherDetailId, row.ProductCode, row.ProductName, row.LotNumber, row.QtyKg,
                    qc == QcDecision.QCFail ? StockType.DefectiveRawMaterial : StockType.RawMaterial));

        var returns = requests.Where(x => x.ReqType == WareHouseRequestType.ExportReturn);
        var headers = await returns.OrderByDescending(x => x.CreatedDate).ThenByDescending(x => x.RequestId).ToListAsync(ct);
        var ids = headers.Select(x => x.RequestId).ToArray();
        var details = await db.WarehouseRequestDetails.AsNoTracking().Where(x => ids.Contains(x.RequestId) && x.IsActive)
            .Select(x => new { x.RequestId, x.ProductCode, x.LotNumber, x.ItemStockType, x.WeightKg }).ToListAsync(ct);
        var movements = await (from r in returns
                               join v in db.WarehouseVouchers.AsNoTracking() on (int?)r.RequestId equals v.RequestId
                               join d in db.WarehouseVoucherDetails.AsNoTracking() on v.VoucherId equals d.VoucherId
                               join l in db.WarehouseShelfLedgers.AsNoTracking() on (long?)d.VoucherDetailId equals l.VoucherDetailId
                               where v.CompanyId == company && v.VoucherType == (int)WareHouseRequestType.ExportReturn && !d.IsIncrease &&
                                   l.CompanyId == company && (!l.VoucherId.HasValue || l.VoucherId == v.VoucherId)
                               select new { r.RequestId, Code = l.ProductCode != null && l.ProductCode.Trim() != "" ? l.ProductCode : d.ProductCode,
                                   Lot = l.LotNumber != null && l.LotNumber.Trim() != "" ? l.LotNumber : d.LotNumber, l.StockType, l.DeltaKg }).ToListAsync(ct);
        var notes = await db.PurchaseOrderDocuments.AsNoTracking().Where(x => orderIds.Contains(x.PurchaseOrderId) && x.IsActive &&
            x.DocumentType == PurchaseOrderDocumentType.Other && x.DocumentName == DocumentName)
            .OrderByDescending(x => x.CreatedDate).ThenByDescending(x => x.PurchaseOrderDocumentId)
            .Select(x => new { x.PurchaseOrderId, x.DocumentCode, x.Note }).ToListAsync(ct);
        var detailsByRequest = details.ToLookup(x => x.RequestId);
        var movementsByRequest = movements.ToLookup(x => x.RequestId);
        var orderIdByCode = unique.ToDictionary(x => x.Code, x => x.PurchaseOrderId);
        foreach (var header in headers)
        {
            var code = Normalize(header.codeFromRequest);
            var fact = facts[code];
            var live = header.IsActive && header.ReqStatus != WarehouseRequestStatus.Cancelled;
            var requested = detailsByRequest[header.RequestId].Where(x => live)
                .GroupBy(x => Key(x.ProductCode, x.LotNumber)).ToDictionary(g => g.Key,
                    g => g.GroupBy(x => (int?)x.ItemStockType ?? 0).ToDictionary(t => t.Key, t => t.Sum(x => Math.Max(0, x.WeightKg))));
            // Sum reversals as well as exports; transfers between shelves net to zero.
            var posted = movementsByRequest[header.RequestId].GroupBy(x => Key(x.Code, x.Lot))
                .ToDictionary(g => g.Key, g => NetPosted(g.Select(x => ((int?)x.StockType ?? 0, x.DeltaKg))));
            decimal requestedKg = detailsByRequest[header.RequestId].Sum(x => Math.Max(0, x.WeightKg)), postedKg = 0, outstandingKg = 0;
            foreach (var lot in requested.Keys.Union(posted.Keys))
            {
                var unshipped = requested.GetValueOrDefault(lot)?.ToDictionary(x => x.Key, x => x.Value) ?? [];
                var shipped = posted.GetValueOrDefault(lot) ?? [];
                postedKg += shipped.Values.Sum();
                var unmatched = 0m;
                foreach (var movement in shipped)
                {
                    var consumed = Math.Min(unshipped.GetValueOrDefault(movement.Key), movement.Value);
                    unshipped[movement.Key] = unshipped.GetValueOrDefault(movement.Key) - consumed;
                    unmatched += movement.Value - consumed;
                    if (movement.Value > 0) fact.Committed.Add(new(lot.Code, lot.Lot, movement.Key == 0 ? null : (StockType)movement.Key, movement.Value));
                }
                foreach (var type in unshipped.Keys.OrderByDescending(x => x).ToArray())
                {
                    var consumed = Math.Min(unshipped[type], unmatched); unmatched -= consumed;
                    var remaining = unshipped[type] - consumed; outstandingKg += remaining;
                    if (remaining > 0) fact.Committed.Add(new(lot.Code, lot.Lot, type == 0 ? null : (StockType)type, remaining));
                }
            }
            var poId = orderIdByCode[code];
            fact.Requests.Add(new(header.RequestId, header.RequestCode, (int)header.ReqStatus, header.CreatedDate,
                notes.FirstOrDefault(x => x.PurchaseOrderId == poId && x.DocumentCode == header.RequestCode)?.Note,
                requestedKg, postedKg, outstandingKg, header.IsActive));
        }
        return facts;
    }
    private static Dictionary<int, decimal> NetPosted(IEnumerable<(int Type, decimal Delta)> rows)
    {
        var entries = rows.ToList();
        var remaining = Math.Max(0, -entries.Sum(x => x.Delta));
        var result = new Dictionary<int, decimal>();
        foreach (var group in entries.GroupBy(x => x.Type).OrderByDescending(x => x.Key))
        {
            var kg = Math.Min(remaining, Math.Max(0, -group.Sum(x => x.Delta)));
            if (kg > 0) result[group.Key] = kg;
            remaining -= kg;
        }
        return result;
    }
    internal static string Normalize(string? code) => (code ?? "").Trim().ToUpperInvariant();
    internal static (string Code, string Lot) Key(string? code, string? lot) => (Normalize(code), lot ?? "");
}
