using HRM.Domain.Enums.Devandqa;
namespace HRM.Application.Features.Reports.PLPU;

public partial class PLPUPurchaseOverviewReportService
{
        private static List<PLPUPurchaseReceiptReportDto> AllocateReceiptsToOrderLines(
            List<PLPUPurchaseOrderLineRaw> orderLines,
            List<PLPUPurchaseReceiptReportDto> receipts)
        {
            var result = new List<PLPUPurchaseReceiptReportDto>();

            if (orderLines.Count == 0 || receipts.Count == 0)
                return result;

            var linesByPoAndMaterial = orderLines
                .GroupBy(x => ReceiptMatchKey(x.POExternalId, x.MaterialCode))
                .ToDictionary(
                    x => x.Key,
                    x => x
                        .OrderBy(r => r.LineNo)
                        .ThenBy(r => r.PurchaseOrderDetailId)
                        .ToList()
                );

            var allocatedPhysicalQtyByLine = orderLines.ToDictionary(
                x => x.PurchaseOrderDetailId,
                _ => 0m
            );

            foreach (var receiptGroup in receipts
                         .OrderBy(x => x.ReceiptDate)
                         .ThenBy(x => x.WarehouseVoucherDetailId)
                         .GroupBy(x => ReceiptMatchKey(x.POExternalId, x.ProductCode)))
            {
                if (!linesByPoAndMaterial.TryGetValue(receiptGroup.Key, out var candidateLines) ||
                    candidateLines.Count == 0)
                {
                    // Không tìm được line tương ứng thì vẫn giữ receipt để debug,
                    // nhưng PurchaseOrderDetailId = null.
                    result.AddRange(receiptGroup);
                    continue;
                }

                foreach (var receipt in receiptGroup)
                {
                    var remainingReceiptQty = receipt.QtyKg;

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
                        var targetRemaining = Positive(targetLine.OrderedQuantity - targetAllocated);

                        var allocatedQty = targetRemaining > 0
                            ? Math.Min(remainingReceiptQty, targetRemaining)
                            : remainingReceiptQty;

                        if (allocatedQty <= 0)
                            allocatedQty = remainingReceiptQty;

                        result.Add(CloneReceiptForLine(receipt, targetLine, allocatedQty));

                        allocatedPhysicalQtyByLine[targetLine.PurchaseOrderDetailId] += allocatedQty;
                        remainingReceiptQty -= allocatedQty;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Clone một dòng nhập kho và gán vào đúng dòng PO với số lượng đã phân bổ.
        /// </summary>
        private static PLPUPurchaseReceiptReportDto CloneReceiptForLine(
            PLPUPurchaseReceiptReportDto source,
            PLPUPurchaseOrderLineRaw line,
            decimal allocatedQty)
        {
            var ratio = source.QtyKg == 0 ? 0 : allocatedQty / source.QtyKg;
            var autoAccept = IsAutoAcceptPackagingQc(line);
            return new PLPUPurchaseReceiptReportDto
            {
                WarehouseRequestId = source.WarehouseRequestId,
                CodeFromRequest = source.CodeFromRequest,

                WarehouseVoucherId = source.WarehouseVoucherId,
                WarehouseVoucherCode = source.WarehouseVoucherCode,
                WarehouseVoucherDetailId = source.WarehouseVoucherDetailId,
                VoucherType = source.VoucherType,

                ReceiptDate = source.ReceiptDate,

                PurchaseOrderId = line.PurchaseOrderId,
                PurchaseOrderDetailId = line.PurchaseOrderDetailId,

                POExternalId = line.POExternalId,
                LineNo = line.LineNo,

                SupplierId = line.SupplierId,
                SupplierName = line.SupplierName,

                ProductCode = source.ProductCode,
                ProductName = string.IsNullOrWhiteSpace(source.ProductName) ? line.MaterialName : source.ProductName,
                LotNumber = source.LotNumber,

                QtyKg = allocatedQty,

                QCResult = source.QCResult,
                QCCreatedDate = source.QCCreatedDate,
                QCGroup = autoAccept ? "Accepted" : source.QCGroup,

                VoucherDetailType = source.VoucherDetailType,

                AcceptedQty = autoAccept
                    ? allocatedQty
                    : Math.Round(source.AcceptedQty * ratio, 6),

                                PendingQcQty = autoAccept
                    ? 0m
                    : Math.Round(source.PendingQcQty * ratio, 6),

                                RejectedQty = autoAccept
                    ? 0m
                    : Math.Round(source.RejectedQty * ratio, 6),
            };
        }

}
