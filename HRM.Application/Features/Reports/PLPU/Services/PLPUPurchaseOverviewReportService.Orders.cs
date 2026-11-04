using HRM.Domain.Enums.Devandqa;
namespace HRM.Application.Features.Reports.PLPU;

public partial class PLPUPurchaseOverviewReportService
{
        private static List<PLPUPurchaseOrderHeaderReportDto> BuildHeaderRows(
            List<PLPUPurchaseOrderLineRaw> orderLines,
            List<PLPUPurchaseOrderDetailReportDto> details)
        {
            var detailsByPo = details
                .GroupBy(x => x.PurchaseOrderId)
                .ToDictionary(x => x.Key, x => x.ToList());

            return orderLines
                .GroupBy(x => x.PurchaseOrderId)
                .Select(g =>
                {
                    var first = g.First();

                    detailsByPo.TryGetValue(first.PurchaseOrderId, out var poDetails);
                    poDetails ??= new List<PLPUPurchaseOrderDetailReportDto>();

                    var ordered = poDetails.Sum(x => x.OrderedQuantity);
                    var received = poDetails.Sum(x => x.WarehouseReceivedQuantity);
                    var accepted = poDetails.Sum(x => x.AcceptedQuantity);
                    var pendingQc = poDetails.Sum(x => x.PendingQcQuantity);
                    var rejected = poDetails.Sum(x => x.RejectedQuantity);

                    var totalValue = g.Sum(x =>
                        x.TotalPriceAgreed ??
                        x.OrderedQuantity * (x.UnitPriceAgreed ?? 0));

                    var receivedValue = poDetails.Sum(x => x.ReceivedPurchaseValue);

                    var flags = ResolveHeaderAlertFlags(poDetails);

                    var reportStatus = ResolveHeaderPrimaryStatus(
                        first.POIsActive,
                        first.POStatus,
                        poDetails,
                        ordered,
                        received,
                        accepted,
                        pendingQc,
                        rejected);

                    var isOverdue = flags.Contains("Overdue");
                    var hasQcFail = flags.Contains("HasQCFail");
                    var hasPendingQc = flags.Contains("WaitingQC");
                    var hasShortage = flags.Contains("Shortage");
                    var hasOpenBalance = !IsQuantityEnough(accepted, ordered);
                    var hasOverDelivery = flags.Contains("OverDelivered");

                    var needAction = IsNeedActionHeader(flags, reportStatus);

                    var deliveryDelayDays = poDetails.Count > 0
                        ? poDetails.Max(x => x.DeliveryDelayDays)
                        : 0;

                    var acceptanceDelayDays = poDetails.Count > 0
                        ? poDetails.Max(x => x.AcceptanceDelayDays)
                        : 0;

                    var lastReceiptDate = poDetails
                        .Where(x => x.LastReceiptDate.HasValue)
                        .Select(x => x.LastReceiptDate)
                        .Max();

                    return new PLPUPurchaseOrderHeaderReportDto
                    {
                        PurchaseOrderId = first.PurchaseOrderId,
                        POExternalId = first.POExternalId,
                        OrderType = first.OrderType,

                        SupplierId = first.SupplierId,
                        SupplierName = first.SupplierName,

                        CompanyId = first.CompanyId,
                        CreatedDate = first.CreatedDate,
                        CreatedBy = first.CreatedBy,

                        RequestDeliveryDate = first.HeaderRequestDeliveryDate,
                        RealDeliveryDate = first.RealDeliveryDate,
                        LastReceiptDate = lastReceiptDate,

                        POStatus = first.POStatus,
                        ReportStatus = reportStatus,
                        AlertFlags = string.Join(",", flags),

                        TotalLines = poDetails.Count,

                        OrderedQuantity = ordered,
                        WarehouseReceivedQuantity = received,
                        AcceptedQuantity = accepted,
                        PendingQcQuantity = pendingQc,
                        RejectedQuantity = rejected,

                        RemainingToReceive = RemainingWithTolerance(ordered, received),
                        RemainingToAccept = RemainingWithTolerance(ordered, accepted),
                        OverReceivedQuantity = OverReceivedWithTolerance(received, ordered),

                        CompletionPercent = Percent(accepted, ordered),
                        WarehouseReceiptPercent = Percent(received, ordered),

                        TotalPurchaseValue = totalValue,
                        ReceivedPurchaseValue = receivedValue,

                        IsOverdue = isOverdue,
                        DeliveryDelayDays = deliveryDelayDays,
                        AcceptanceDelayDays = acceptanceDelayDays,
                        DelayDays = deliveryDelayDays,

                        HasQCFail = hasQcFail,
                        HasPendingQC = hasPendingQc,
                        HasShortage = hasShortage,
                        HasOpenBalance = hasOpenBalance,
                        HasOverDelivery = hasOverDelivery,

                        NeedBuyerAction = needAction,
                        BuyerAction = ResolveBuyerAction(flags),

                        PLPUComment = first.PLPUComment,
                        Comment = first.Comment
                    };
                })
                .ToList();
        }

}
