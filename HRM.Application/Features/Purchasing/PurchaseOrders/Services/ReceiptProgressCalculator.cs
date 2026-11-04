using HRM.Application.Features.Purchasing.PurchaseOrders.Dtos;
using HRM.Domain.Enums.Devandqa;
using HRM.Domain.Enums.WareHouses;

namespace HRM.Application.Features.Purchasing.PurchaseOrders.Services;

internal sealed record ReceiptOrderedLine(Guid Id, string Code, string Name, string? Unit, decimal Quantity);
internal sealed record ReceiptInspection(long DetailId, Guid Id, DateTime Date, QcDecision? Decision);
internal sealed record ReceiptMovement(long DetailId, long? VoucherId, int SlotId, string? SlotCode,
    string? ProductCode, string? Lot, StockType? StockType, decimal Delta, DateTime Date);

internal static class ReceiptProgressCalculator
{
    private static string Key(string? value) => (value ?? "").Trim().ToUpperInvariant();

    internal static void ApplyEvidence(List<ReceiptVoucherLineDto> rows,
        IReadOnlyList<ReceiptInspection> inspections, IReadOnlyList<ReceiptMovement> movements)
    {
        var qcByDetail = inspections.ToLookup(x => x.DetailId);
        var ledgerByDetail = movements.ToLookup(x => x.DetailId);
        foreach (var row in rows)
        {
            row.Included = Key(row.VoucherStatus) is not ("CANCELED" or "CANCELLED") && row.RecordedQuantity >= 0;
            if (!row.Included) row.Warnings.Add("ExcludedCanceledOrNegativeLine");
            var qc = qcByDetail[row.VoucherDetailId].OrderByDescending(x => x.Date).ThenByDescending(x => x.Id).ToList();
            var latest = qc.FirstOrDefault();
            row.QcId = latest?.Id;
            row.QcDate = latest?.Date;
            row.QcStatus = latest?.Decision switch
            {
                QcDecision.Waiter => "OnHold", QcDecision.QCPass => "Passed", QcDecision.QCFail => "Failed",
                QcDecision.Special => "SpecialAccepted", null => "NotInspected", _ => "Unknown"
            };
            if (qc.Count > 1) row.Warnings.Add("MultipleQcRecordsLatestSelected");
            var source = ledgerByDetail[row.VoucherDetailId].ToList();
            var ledger = source.Where(x => (!x.VoucherId.HasValue || x.VoucherId == row.VoucherId) &&
                (string.IsNullOrWhiteSpace(x.ProductCode) || Key(x.ProductCode) == Key(row.ProductCode))).ToList();
            if (ledger.Count != source.Count) row.Warnings.Add("InconsistentLedgerReference");
            // Initial +100, QC transfer -100 Waiter/+100 RawMaterial => net 100, never 200.
            // Outbound consumption has its own voucher-detail and is not subtracted from receipt progress.
            row.PostedQuantity = ledger.Sum(x => x.Delta);
            row.LastPostingDate = ledger.Count == 0 ? null : ledger.Max(x => x.Date);
            row.Postings = ledger.GroupBy(x => new { x.SlotId, x.SlotCode, x.Lot, x.StockType })
                .OrderBy(x => x.Key.SlotId).ThenBy(x => x.Key.Lot).ThenBy(x => x.Key.StockType)
                .Select(x => new ReceiptPostingDto(x.Key.SlotId, x.Key.SlotCode, x.Key.Lot,
                    x.Key.StockType.HasValue ? (int)x.Key.StockType.Value : null, x.Sum(y => y.Delta))).ToList();
            if (ledger.Count > 0 && row.PostedQuantity != row.RecordedQuantity) row.Warnings.Add("LedgerQuantityDiffersFromVoucher");
            if (row.PostedQuantity < 0) row.Warnings.Add("NegativeNetPosting");
            if (row.IsApplied && ledger.Count == 0) row.Warnings.Add("AppliedWithoutLedger");
            if (!row.IsApplied && ledger.Count > 0) row.Warnings.Add("LedgerWithoutAppliedFlag");
        }
    }

    internal static PurchaseOrderReceiptProgressDto Build(PurchaseOrderReceiptProgressDto result,
        IReadOnlyList<ReceiptOrderedLine> ordered, List<ReceiptVoucherLineDto> receipts)
    {
        result.Receipts = receipts;
        var associationKnown = result.AssociationStatus is "Matched" or "NoRequests";
        result.Materials = ordered.GroupBy(x => new { Code = Key(x.Code), Unit = Key(x.Unit) })
            .Select(g =>
            {
                var first = g.First();
                var line = new ReceiptMaterialProgressDto { ProductCode = first.Code.Trim(), ProductName = first.Name,
                    Unit = first.Unit, OrderedQuantity = g.Sum(x => x.Quantity), PurchaseOrderDetailIds = g.Select(x => x.Id).ToList() };
                if (!associationKnown) return line; // Unknown source is not a confirmed zero receipt.
                var matched = receipts.Where(x => x.Included && Key(x.ProductCode).Length > 0 &&
                    Key(x.ProductCode) == g.Key.Code && Key(x.Unit) == g.Key.Unit).ToList();
                line.RequiresReview = matched.Any(x => x.Warnings.Count > 0);
                line.RecordedQuantity = matched.Sum(x => x.RecordedQuantity);
                line.PostedQuantity = matched.Sum(x => x.PostedQuantity);
                line.RemainingToPost = Math.Max(0, line.OrderedQuantity - line.PostedQuantity.Value);
                line.OverPostedQuantity = Math.Max(0, line.PostedQuantity.Value - line.OrderedQuantity);
                var unreliablePosting = matched.Any(x => x.Warnings.Any(w => w is "InconsistentLedgerReference" or
                    "LedgerQuantityDiffersFromVoucher" or "NegativeNetPosting"));
                line.PostingPercent = line.OrderedQuantity <= 0 || unreliablePosting ? null : Math.Round(line.PostedQuantity.Value * 100 / line.OrderedQuantity, 2);
                line.NotInspectedQuantity = matched.Where(x => x.QcStatus == "NotInspected").Sum(x => x.RecordedQuantity);
                line.OnHoldQuantity = matched.Where(x => x.QcStatus == "OnHold").Sum(x => x.RecordedQuantity);
                line.PassedQuantity = matched.Where(x => x.QcStatus == "Passed").Sum(x => x.RecordedQuantity);
                line.FailedQuantity = matched.Where(x => x.QcStatus == "Failed").Sum(x => x.RecordedQuantity);
                line.SpecialAcceptedQuantity = matched.Where(x => x.QcStatus == "SpecialAccepted").Sum(x => x.RecordedQuantity);
                line.UnknownQcQuantity = matched.Where(x => x.QcStatus == "Unknown").Sum(x => x.RecordedQuantity);
                return line;
            }).ToList();
        var keys = ordered.Where(x => Key(x.Code).Length > 0).Select(x => (Key(x.Code), Key(x.Unit))).ToHashSet();
        foreach (var receipt in receipts.Where(x => !keys.Contains((Key(x.ProductCode), Key(x.Unit)))))
            receipt.Warnings.Add("UnmatchedOrderMaterialOrUnit");
        if (ordered.Any(x => Key(x.Code).Length == 0)) result.Warnings.Add("OrderLineMissingProductCode");
        if (result.Materials.Any(x => x.PurchaseOrderDetailIds.Count > 1)) result.Warnings.Add("RepeatedOrderMaterialGrouped");
        if (receipts.Any(x => x.Warnings.Count > 0)) result.Warnings.Add("ReceiptEvidenceRequiresReview");
        return result;
    }
}
