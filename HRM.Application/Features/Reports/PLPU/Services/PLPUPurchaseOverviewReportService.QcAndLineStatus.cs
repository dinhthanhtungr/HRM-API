using HRM.Domain.Enums.Devandqa;
namespace HRM.Application.Features.Reports.PLPU;

public partial class PLPUPurchaseOverviewReportService
{
        private static List<PLPUPurchaseQcReportDto> BuildQcRowsFromAllocatedReceipts(
            List<PLPUPurchaseReceiptReportDto> receipts)
        {
            return receipts
                .Select(receipt =>
                {
                    var qcGroup = !string.IsNullOrWhiteSpace(receipt.QCGroup)
                        ? receipt.QCGroup
                        : receipt.QCResult is HRM.Domain.Enums.Devandqa.QcDecision.QCPass
                            or HRM.Domain.Enums.Devandqa.QcDecision.Special
                                ? "Accepted"
                                : receipt.QCResult == HRM.Domain.Enums.Devandqa.QcDecision.QCFail
                                    ? "Rejected"
                                    : "Pending";

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
                        QCGroup = qcGroup,

                        ReceiptDate = receipt.ReceiptDate,
                        QCCreatedDate = receipt.QCCreatedDate,

                        QCAgeDays = qcGroup == "Pending"
                            ? Math.Max((DateTime.Today - receipt.ReceiptDate.Date).Days, 0)
                            : 0,

                        BuyerAction = qcGroup == "Rejected" ? "Handle QC fail with supplier"
                            : qcGroup == "Pending" ? "Follow up QC decision"
                            : string.Empty
                    };
                })
                .ToList();
        }

        /// <summary>
        /// Tìm ngày mà số lượng cộng dồn đã đủ theo số lượng đặt.
        /// </summary>
        private static DateTime? GetDateWhenEnough(
            IEnumerable<PLPUPurchaseReceiptReportDto> receipts,
            decimal orderedQuantity,
            Func<PLPUPurchaseReceiptReportDto, decimal> quantitySelector)
        {
            if (orderedQuantity <= 0)
                return null;

            decimal running = 0;

            foreach (var receipt in receipts.OrderBy(x => x.ReceiptDate))
            {
                running += quantitySelector(receipt);

                if (running >= orderedQuantity)
                    return receipt.ReceiptDate;
            }

            return null;
        }

        /// <summary>
        /// Tính số ngày trễ so với ngày yêu cầu.
        /// </summary>
        private static int CalculateDelayDays(
            DateTime? dueDate,
            DateTime? actualEnoughDate,
            decimal currentQuantity,
            decimal orderedQuantity)
        {
            if (!dueDate.HasValue || orderedQuantity <= 0)
                return 0;

            var comparisonDate = IsQuantityEnough(currentQuantity, orderedQuantity) && actualEnoughDate.HasValue
                ? actualEnoughDate.Value.Date
                : DateTime.Today;

            if (actualEnoughDate.HasValue && IsBeforeNextDay(actualEnoughDate.Value, dueDate.Value))
                return 0;

            return Math.Max((comparisonDate - dueDate.Value.Date).Days, 0);
        }

        /// <summary>
        /// Xác định trạng thái chính của một dòng PO.
        /// </summary>
        private static string ResolveLinePrimaryStatus(
            bool isActive,
            string poStatus,
            decimal ordered,
            decimal received,
            decimal accepted,
            decimal pendingQc,
            decimal rejected,
            DateTime? dueDate)
        {
            if (!isActive || IsCancelledOrInactiveStatus(poStatus))
                return "Cancelled";

            if (IsClosedStatus(poStatus))
            {
                if (ordered > 0 && !IsQuantityEnough(accepted, ordered))
                    return "ClosedWithShortage";

                return "Closed";
            }

            if (ordered <= 0)
                return "NoQuantity";

            if (IsQuantityEnough(accepted, ordered))
                return "AcceptedEnough";

            if (IsQuantityEnough(received, ordered) && pendingQc > 0)
                return "DeliveredEnoughWaitingQC";

            if (IsQuantityEnough(received, ordered))
                return "DeliveredEnough";

            if (accepted > 0)
                return "PartiallyAccepted";

            if (received > 0)
                return "PartiallyDelivered";

            if (dueDate.HasValue && DateTime.Today > dueDate.Value.Date)
                return "OverdueNotDelivered";

            return "NotDelivered";
        }

        /// <summary>
        /// Xác định các cảnh báo của một dòng PO.
        /// </summary>
        private static List<string> ResolveLineAlertFlags(
            decimal ordered,
            decimal received,
            decimal accepted,
            decimal pendingQc,
            decimal rejected,
            DateTime? dueDate,
            DateTime? dateWhenReceivedEnough)
        {
            var flags = new List<string>();

            if (ordered <= 0)
                return flags;

            if (dueDate.HasValue &&
                ((DateTime.Today > dueDate.Value.Date && !IsQuantityEnough(received, ordered)) ||
                 (dateWhenReceivedEnough.HasValue && dateWhenReceivedEnough.Value.Date > dueDate.Value.Date)))
            {
                flags.Add("Overdue");

                if (!IsQuantityEnough(received, ordered))
                    flags.Add("Shortage");
            }

            if (rejected > 0)
                flags.Add("HasQCFail");

            if (pendingQc > 0)
                flags.Add("WaitingQC");

            if (IsOverDelivered(received, ordered))
                flags.Add("OverDelivered");

            return flags.Distinct().ToList();
        }

}
