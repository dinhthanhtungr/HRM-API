using HRM.Application.Abstractions.Persistence.Purchasing;
using HRM.Domain.Enums.Devandqa;
using HRM.Domain.Enums.WareHouses;
using Microsoft.EntityFrameworkCore;
namespace HRM.Application.Features.Reports.PLPU;
public partial class PLPUPurchaseOverviewReportRepository
{
        private static List<PLPUPurchaseReceiptReportDto> NormalizeReceiptRows(
            IEnumerable<PLPUPurchaseReceiptReportDto> rows)
        {
            return rows
                .GroupBy(x => new
                {
                    x.PurchaseOrderId,
                    POExternalId = NormalizeKey(x.POExternalId),
                    x.SupplierId,
                    x.SupplierName,
                    x.WarehouseRequestId,
                    CodeFromRequest = NormalizeKey(x.CodeFromRequest),
                    x.WarehouseVoucherId,
                    x.WarehouseVoucherCode,
                    x.WarehouseVoucherDetailId,
                    x.VoucherType,
                    ProductCode = NormalizeKey(x.ProductCode),
                    ProductName = NormalizeKey(x.ProductName),
                    LotNumber = NormalizeKey(x.LotNumber),
                    x.QCResult,
                    x.QCCreatedDate,
                    x.VoucherDetailType
                })
                .Select(g =>
                {
                    var first = g.First();

                    return new PLPUPurchaseReceiptReportDto
                    {
                        WarehouseRequestId = g.Key.WarehouseRequestId,
                        CodeFromRequest = first.CodeFromRequest,

                        WarehouseVoucherId = g.Key.WarehouseVoucherId,
                        WarehouseVoucherCode = first.WarehouseVoucherCode,

                        WarehouseVoucherDetailId = g.Key.WarehouseVoucherDetailId,
                        VoucherType = g.Key.VoucherType,

                        ReceiptDate = g.Min(x => x.ReceiptDate),

                        PurchaseOrderId = g.Key.PurchaseOrderId,
                        PurchaseOrderDetailId = null,

                        POExternalId = first.POExternalId,
                        LineNo = 0,

                        SupplierId = g.Key.SupplierId,
                        SupplierName = first.SupplierName,

                        ProductCode = first.ProductCode,
                        ProductName = first.ProductName,
                        LotNumber = first.LotNumber,

                        QtyKg = g.Sum(x => x.QtyKg),

                        QCResult = g.Key.QCResult,
                        QCCreatedDate = g.Key.QCCreatedDate,

                        VoucherDetailType = g.Key.VoucherDetailType
                    };
                })
                .ToList();
        }

        private static PLPUPurchaseReceiptReportDto ApplyReceiptQuantities(PLPUPurchaseReceiptReportDto receipt)
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

            receipt.AcceptedQty = decision is QcDecision.QCPass or QcDecision.Special
                ? receipt.QtyKg
                : 0;

            receipt.PendingQcQty = !decision.HasValue || decision == QcDecision.Waiter
                ? receipt.QtyKg
                : 0;

            receipt.RejectedQty = decision == QcDecision.QCFail
                ? receipt.QtyKg
                : 0;

            receipt.QCGroup = decision is QcDecision.QCPass or QcDecision.Special ? "Accepted"
                : decision == QcDecision.QCFail ? "Rejected"
                : "Pending";

            return receipt;
        }

        private static PLPUPurchaseQcReportDto ToQcRow(PLPUPurchaseReceiptReportDto receipt)
        {
            return new PLPUPurchaseQcReportDto
            {
                QCInputByQCId = null,
                VoucherDetailId = receipt.WarehouseVoucherDetailId,

                PurchaseOrderId = receipt.PurchaseOrderId,
                PurchaseOrderDetailId = receipt.PurchaseOrderDetailId,

                POExternalId = receipt.POExternalId,
                LineNo = receipt.LineNo,

                SupplierId = receipt.SupplierId,
                SupplierName = receipt.SupplierName,

                MaterialCode = receipt.ProductCode,
                MaterialName = receipt.ProductName,
                LotNumber = receipt.LotNumber,

                QtyKg = receipt.QtyKg,

                QCResult = receipt.QCResult,
                QCGroup = receipt.QCGroup,

                ReceiptDate = receipt.ReceiptDate,
                QCCreatedDate = receipt.QCCreatedDate
            };
        }

        private static PLPUPurchaseQcReportDto ApplyQcGroup(PLPUPurchaseQcReportDto row)
        {
            row.QCGroup = row.QCResult is QcDecision.QCPass or QcDecision.Special ? "Accepted"
                : row.QCResult == QcDecision.QCFail ? "Rejected"
                : "Pending";

            row.QCAgeDays = row.QCGroup == "Pending"
                ? Math.Max((DateTime.Today - row.ReceiptDate.Date).Days, 0)
                : 0;

            row.BuyerAction = row.QCGroup == "Rejected" ? "Handle QC fail with supplier"
                : row.QCGroup == "Pending" ? "Follow up QC decision"
                : string.Empty;

            return row;
        }

        private static string NormalizeKey(string? value)
        {
            return (value ?? string.Empty).Trim().ToUpperInvariant();
        }

}
