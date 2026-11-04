using HRM.Domain.Enums.Devandqa;
namespace HRM.Application.Features.Reports.PLPU;

public partial class PLPUPurchaseOverviewReportService
{
        private static List<PLPUPurchaseOrderDetailReportDto> BuildDetailRows(
            List<PLPUPurchaseOrderLineRaw> orderLines,
            List<PLPUPurchaseReceiptReportDto> allocatedReceipts)
        {
            var receiptsByDetailId = allocatedReceipts
                .Where(x => x.PurchaseOrderDetailId.HasValue)
                .GroupBy(x => x.PurchaseOrderDetailId!.Value)
                .ToDictionary(x => x.Key, x => x.OrderBy(r => r.ReceiptDate).ToList());

            return orderLines
                .Select(line =>
                {
                    receiptsByDetailId.TryGetValue(line.PurchaseOrderDetailId, out var receipts);
                    receipts ??= new List<PLPUPurchaseReceiptReportDto>();

                    var received = receipts.Sum(x => x.QtyKg);
                    var accepted = receipts.Sum(x => x.AcceptedQty);
                    var pendingQc = receipts.Sum(x => x.PendingQcQty);
                    var rejected = receipts.Sum(x => x.RejectedQty);

                    if (IsAutoAcceptPackagingQc(line))
                    {
                        accepted = received;
                        pendingQc = 0m;
                        rejected = 0m;
                    }

                    var dueDate = line.DeliveryDate ?? line.HeaderRequestDeliveryDate;

                    var firstReceipt = receipts.Count > 0 ? receipts.Min(x => x.ReceiptDate) : (DateTime?)null;
                    var lastReceipt = receipts.Count > 0 ? receipts.Max(x => x.ReceiptDate) : (DateTime?)null;

                    var dateWhenReceivedEnough = GetDateWhenEnough(
                        receipts.OrderBy(x => x.ReceiptDate),
                        line.OrderedQuantity,
                        x => x.QtyKg);

                    var dateWhenAcceptedEnough = GetDateWhenEnough(
                        receipts.OrderBy(x => x.ReceiptDate),
                        line.OrderedQuantity,
                        x => IsAutoAcceptPackagingQc(line) ? x.QtyKg : x.AcceptedQty);

                    var deliveryDelayDays = CalculateDelayDays(
                        dueDate,
                        dateWhenReceivedEnough,
                        received,
                        line.OrderedQuantity);

                    var acceptanceDelayDays = CalculateDelayDays(
                        dueDate,
                        dateWhenAcceptedEnough,
                        accepted,
                        line.OrderedQuantity);

                    var priceVariance = line.UnitPriceAgreed.HasValue && line.BaseCostSnapshot.HasValue
                        ? line.UnitPriceAgreed.Value - line.BaseCostSnapshot.Value
                        : (decimal?)null;

                    var effectiveUnitPrice = ResolveUnitPrice(line.OrderedQuantity, line.UnitPriceAgreed, line.TotalPriceAgreed, line.BaseCostSnapshot);
                    var receivedPurchaseValue = Math.Round(received * effectiveUnitPrice, 2, MidpointRounding.AwayFromZero);

                    var priceVariancePercent = priceVariance.HasValue &&
                                               line.BaseCostSnapshot.HasValue &&
                                               line.BaseCostSnapshot.Value != 0
                        ? priceVariance.Value / line.BaseCostSnapshot.Value * 100
                        : (decimal?)null;

                    var flags = ResolveLineAlertFlags(
                        ordered: line.OrderedQuantity,
                        received: received,
                        accepted: accepted,
                        pendingQc: pendingQc,
                        rejected: rejected,
                        dueDate: dueDate,
                        dateWhenReceivedEnough: dateWhenReceivedEnough);

                    var status = ResolveLinePrimaryStatus(
                        isActive: line.POIsActive,
                        poStatus: line.POStatus,
                        ordered: line.OrderedQuantity,
                        received: received,
                        accepted: accepted,
                        pendingQc: pendingQc,
                        rejected: rejected,
                        dueDate: dueDate);

                    var needAction = IsNeedActionLine(flags);



                    return new PLPUPurchaseOrderDetailReportDto
                    {
                        PurchaseOrderDetailId = line.PurchaseOrderDetailId,
                        PurchaseOrderId = line.PurchaseOrderId,
                        POExternalId = line.POExternalId,

                        LineNo = line.LineNo,

                        MaterialId = line.MaterialId,
                        MaterialCode = line.MaterialCode,
                        MaterialName = line.MaterialName,
                        Package = line.Package,

                        OrderedQuantity = line.OrderedQuantity,
                        WarehouseReceivedQuantity = received,
                        AcceptedQuantity = accepted,
                        PendingQcQuantity = pendingQc,
                        RejectedQuantity = rejected,

                        RemainingToReceive = RemainingWithTolerance(line.OrderedQuantity, received),
                        RemainingToAccept = RemainingWithTolerance(line.OrderedQuantity, accepted),
                        OverReceivedQuantity = OverReceivedWithTolerance(received, line.OrderedQuantity),

                        DeliveryDate = line.DeliveryDate,
                        RequestDeliveryDate = dueDate,

                        FirstReceiptDate = firstReceipt,
                        LastReceiptDate = lastReceipt,
                        DateWhenReceivedEnough = dateWhenReceivedEnough,
                        DateWhenAcceptedEnough = dateWhenAcceptedEnough,

                        DeliveryDelayDays = deliveryDelayDays,
                        AcceptanceDelayDays = acceptanceDelayDays,
                        DelayDays = deliveryDelayDays,

                        LineReportStatus = status,
                        AlertFlags = string.Join(",", flags),

                        NeedBuyerAction = needAction,
                        BuyerAction = ResolveBuyerAction(flags),

                        BaseCostSnapshot = line.BaseCostSnapshot,
                        BaseDateSnapshot = line.BaseDateSnapshot,
                        UnitPriceAgreed = line.UnitPriceAgreed,
                        TotalPriceAgreed = line.TotalPriceAgreed,
                        ReceivedPurchaseValue = receivedPurchaseValue,

                        PriceVariance = priceVariance,
                        PriceVariancePercent = priceVariancePercent,
                        PriceImpactAmount = priceVariance.HasValue
                            ? priceVariance.Value * line.OrderedQuantity
                            : null,

                        Note = line.Note
                    };
                })
                .ToList();
        }

}
