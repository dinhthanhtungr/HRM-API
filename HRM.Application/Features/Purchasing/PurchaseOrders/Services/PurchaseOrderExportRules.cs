using HRM.Application.Abstractions.Persistence.Purchasing;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.Purchasing.PurchaseOrders.Dtos;
using HRM.Domain.Enums.Devandqa;
using HRM.Domain.Enums.WareHouses;
using Microsoft.EntityFrameworkCore;
namespace HRM.Application.Features.Purchasing.PurchaseOrders.Services;

internal static class PurchaseOrderExportRules
{
    public const string ReportOrderType = "NCC";
    public static List<PurchaseOrderReceiptExportRow> AllocatePurchaseOrderReceipts(
        List<PurchaseOrderExportLine> orderLines,
        List<PurchaseOrderReceiptExportRow> receipts)
    {
        var result = new List<PurchaseOrderReceiptExportRow>();

        var linesByPoAndMaterial = orderLines
            .GroupBy(x => ReceiptMatchKey(x.POExternalId, x.MaterialCode))
            .ToDictionary(
                x => x.Key,
                x => x
                    .OrderBy(r => r.LineNo)
                    .ThenBy(r => r.PurchaseOrderDetailId)
                    .ToList());

        var allocatedPhysicalQtyByLine = orderLines.ToDictionary(
            x => x.PurchaseOrderDetailId,
            _ => 0m);

        foreach (var receiptGroup in receipts
                     .OrderBy(x => x.ReceiptDate)
                     .ThenBy(x => x.VoucherDetailId)
                     .GroupBy(x => ReceiptMatchKey(x.POExternalId, x.ProductCode)))
        {
            if (!linesByPoAndMaterial.TryGetValue(receiptGroup.Key, out var candidateLines) ||
                candidateLines.Count == 0)
            {
                result.AddRange(receiptGroup);
                continue;
            }

            foreach (var receipt in receiptGroup)
            {
                var remainingReceiptQty = receipt.ReceivedQuantity;

                if (remainingReceiptQty <= 0)
                    continue;

                while (remainingReceiptQty > 0)
                {
                    var targetLine = candidateLines.FirstOrDefault(line =>
                    {
                        var allocated = allocatedPhysicalQtyByLine[line.PurchaseOrderDetailId];
                        return allocated < line.OrderedQuantity;
                    });

                    targetLine ??= candidateLines.Last();

                    var targetAllocated = allocatedPhysicalQtyByLine[targetLine.PurchaseOrderDetailId];
                    var targetRemaining = Math.Max(targetLine.OrderedQuantity - targetAllocated, 0m);
                    var allocatedQty = targetRemaining > 0
                        ? Math.Min(remainingReceiptQty, targetRemaining)
                        : remainingReceiptQty;

                    if (allocatedQty <= 0)
                        allocatedQty = remainingReceiptQty;

                    result.Add(ClonePurchaseOrderReceiptForLine(receipt, targetLine, allocatedQty));
                    allocatedPhysicalQtyByLine[targetLine.PurchaseOrderDetailId] += allocatedQty;
                    remainingReceiptQty -= allocatedQty;
                }
            }
        }

        return result;
    }

    public static PurchaseOrderReceiptExportRow ClonePurchaseOrderReceiptForLine(
        PurchaseOrderReceiptExportRow source,
        PurchaseOrderExportLine line,
        decimal allocatedQuantity)
    {
        var ratio = source.ReceivedQuantity == 0m
            ? 0m
            : allocatedQuantity / source.ReceivedQuantity;

        return new PurchaseOrderReceiptExportRow
        {
            POExternalId = line.POExternalId,
            PurchaseOrderDetailId = line.PurchaseOrderDetailId,
            VoucherDetailId = source.VoucherDetailId,
            ProductCode = source.ProductCode,
            ProductName = source.ProductName,
            LotNumber = source.LotNumber,
            ReceiptDate = source.ReceiptDate,
            ReceivedQuantity = allocatedQuantity,
            QCResult = source.QCResult,
            VoucherDetailType = source.VoucherDetailType,
            AcceptedQuantity = Math.Round(source.AcceptedQuantity * ratio, 6),
            PendingQcQuantity = Math.Round(source.PendingQcQuantity * ratio, 6),
            RejectedQuantity = Math.Round(source.RejectedQuantity * ratio, 6)
        };
    }

    public static void ApplyPurchaseOrderReceiptQcQuantities(PurchaseOrderReceiptExportRow receipt)
    {
        var decision = receipt.QCResult;

        if (!decision.HasValue)
        {
            decision = receipt.VoucherDetailType == VoucherDetailType.QCPass ? QcDecision.QCPass
                : receipt.VoucherDetailType == VoucherDetailType.Special ? QcDecision.Special
                : receipt.VoucherDetailType == VoucherDetailType.QCFail ? QcDecision.QCFail
                : receipt.VoucherDetailType == VoucherDetailType.Waiter ? QcDecision.Waiter
                : null;
        }

        receipt.QCResult = decision;
        receipt.AcceptedQuantity = decision is QcDecision.QCPass or QcDecision.Special
            ? receipt.ReceivedQuantity
            : 0m;
        receipt.PendingQcQuantity = !decision.HasValue || decision == QcDecision.Waiter
            ? receipt.ReceivedQuantity
            : 0m;
        receipt.RejectedQuantity = decision == QcDecision.QCFail
            ? receipt.ReceivedQuantity
            : 0m;
    }

    public static decimal ResolvePurchaseOrderUnitPrice(
        decimal orderedQuantity,
        decimal? unitPriceAgreed,
        decimal? totalPriceAgreed,
        decimal? baseCostSnapshot = null)
    {
        if (unitPriceAgreed.HasValue)
            return unitPriceAgreed.Value;

        if (totalPriceAgreed.HasValue && orderedQuantity > 0m)
            return totalPriceAgreed.Value / orderedQuantity;

        return baseCostSnapshot ?? 0m;
    }

    public static int? CalculateLateDays(DateTime? requestDeliveryDate, DateTime? actualDeliveryDate)
    {
        if (!requestDeliveryDate.HasValue || !actualDeliveryDate.HasValue)
            return null;

        return Math.Max((actualDeliveryDate.Value.Date - requestDeliveryDate.Value.Date).Days, 0);
    }

    public static string ResolveDeliveryResult(DateTime? actualDeliveryDate, int? lateDays)
    {
        if (!actualDeliveryDate.HasValue)
            return "Chưa giao";

        return (lateDays ?? 0) > 0 ? "Không đạt" : "Đạt";
    }

    public static string ReceiptMatchKey(string? poExternalId, string? materialCode)
    {
        return $"{NormalizePurchaseOrderKey(poExternalId)}|{NormalizePurchaseOrderKey(materialCode)}";
    }

    public static string NormalizePurchaseOrderKey(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToUpperInvariant();
    }

    public static decimal ResolveRealTotalAmount(
        decimal receivedQuantity,
        decimal unitPrice)
    {
        // Giá trị thực nhận theo lượng nhập ledger, không phụ thuộc QC hoặc loại PO.
        return receivedQuantity * unitPrice;
    }

}
