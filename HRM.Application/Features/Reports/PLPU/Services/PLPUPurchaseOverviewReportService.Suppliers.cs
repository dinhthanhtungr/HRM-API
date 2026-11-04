using HRM.Domain.Enums.Devandqa;
namespace HRM.Application.Features.Reports.PLPU;

public partial class PLPUPurchaseOverviewReportService
{
        private static List<PLPUSupplierPurchasePerformanceDto> BuildSupplierPerformance(
            List<PLPUPurchaseOrderLineRaw> orderLines,
            List<PLPUPurchaseOrderDetailReportDto> details,
            List<PLPUPurchaseOrderHeaderReportDto> headers)
        {
            var lineById = orderLines.ToDictionary(x => x.PurchaseOrderDetailId, x => x);
            var headerByPo = headers.ToDictionary(x => x.PurchaseOrderId, x => x);

            return details
                .Where(x => lineById.ContainsKey(x.PurchaseOrderDetailId))
                .GroupBy(x => lineById[x.PurchaseOrderDetailId].SupplierId)
                .Select(g =>
                {
                    var firstLine = lineById[g.First().PurchaseOrderDetailId];

                    var poIds = g.Select(x => x.PurchaseOrderId).Distinct().ToList();

                    var ordered = g.Sum(x => x.OrderedQuantity);
                    var received = g.Sum(x => x.WarehouseReceivedQuantity);
                    var accepted = g.Sum(x => x.AcceptedQuantity);
                    var rejected = g.Sum(x => x.RejectedQuantity);
                    var pending = g.Sum(x => x.PendingQcQuantity);

                    var totalValue = g.Sum(x =>
                        x.TotalPriceAgreed ??
                        x.OrderedQuantity * (x.UnitPriceAgreed ?? 0));

                    var receivedValue = g.Sum(x => x.ReceivedPurchaseValue);
                    var priceVarianceAmount = g.Sum(x => x.PriceImpactAmount ?? 0);

                    var deliveryEvaluable = g.Where(IsDeliveryOtdEvaluable).ToList();
                    var acceptanceEvaluable = g.Where(IsAcceptanceOtdEvaluable).ToList();

                    var riskReasons = BuildSupplierRiskReasons(g.ToList(), headerByPo);

                    var supplierDeliveryOtd = Percent(
                        deliveryEvaluable.Count(IsDeliveryOnTime),
                        deliveryEvaluable.Count);

                    var supplierAcceptanceOtd = Percent(
                        acceptanceEvaluable.Count(IsAcceptanceOnTime),
                        acceptanceEvaluable.Count);

                    return new PLPUSupplierPurchasePerformanceDto
                    {
                        SupplierId = firstLine.SupplierId,
                        SupplierName = firstLine.SupplierName,

                        TotalPO = poIds.Count,
                        TotalLines = g.Count(),

                        TotalPurchaseValue = totalValue,
                        ReceivedPurchaseValue = receivedValue,

                        OrderedQuantity = ordered,
                        WarehouseReceivedQuantity = received,
                        AcceptedQuantity = accepted,
                        PendingQcQuantity = pending,
                        RejectedQuantity = rejected,

                        SupplierOTD = supplierDeliveryOtd,
                        SupplierDeliveryOTD = supplierDeliveryOtd,
                        SupplierAcceptanceOTD = supplierAcceptanceOtd,

                        SupplierFillRate = Percent(g.Sum(x => Math.Min(x.WarehouseReceivedQuantity, x.OrderedQuantity)), ordered),
                        SupplierAcceptanceRate = Percent(accepted, received),
                        SupplierQcFailRate = Percent(rejected, received),

                        AverageDeliveryDelayDays = deliveryEvaluable.Count > 0
                            ? Math.Round((decimal)deliveryEvaluable.Average(x => x.DeliveryDelayDays), 2)
                            : 0,

                        AverageAcceptanceDelayDays = acceptanceEvaluable.Count > 0
                            ? Math.Round((decimal)acceptanceEvaluable.Average(x => x.AcceptanceDelayDays), 2)
                            : 0,

                        AverageDelayDays = deliveryEvaluable.Count > 0
                            ? Math.Round((decimal)deliveryEvaluable.Average(x => x.DeliveryDelayDays), 2)
                            : 0,

                        AverageUnitPrice = ordered == 0
                            ? 0
                            : Math.Round(totalValue / ordered, 4),

                        PriceVarianceAmount = priceVarianceAmount,

                        RiskScore = riskReasons.Sum(x => x.ImpactScore),
                        RiskReasons = riskReasons
                    };
                })
                .ToList();
        }

        private static List<PLPUSupplierRiskReasonDto> BuildSupplierRiskReasons(
            List<PLPUPurchaseOrderDetailReportDto> details,
            Dictionary<Guid, PLPUPurchaseOrderHeaderReportDto> headerByPo)
        {
            var reasons = new List<PLPUSupplierRiskReasonDto>();

            foreach (var poGroup in details.GroupBy(x => x.PurchaseOrderId))
            {
                if (!headerByPo.TryGetValue(poGroup.Key, out var header) || !header.IsOverdue)
                    continue;

                var detail = poGroup
                    .OrderByDescending(x => x.DeliveryDelayDays)
                    .ThenByDescending(x => x.RemainingToReceive)
                    .FirstOrDefault();

                reasons.Add(new PLPUSupplierRiskReasonDto
                {
                    PurchaseOrderId = header.PurchaseOrderId,
                    POExternalId = header.POExternalId,
                    PurchaseOrderDetailId = detail?.PurchaseOrderDetailId,
                    LineNo = detail?.LineNo,
                    MaterialCode = detail?.MaterialCode ?? string.Empty,
                    MaterialName = detail?.MaterialName ?? string.Empty,
                    ReasonCode = "Overdue",
                    ReasonText = "PO quá hạn giao theo lượng nhập kho",
                    ImpactScore = 30,
                    OrderedQuantity = detail?.OrderedQuantity ?? header.OrderedQuantity,
                    WarehouseReceivedQuantity = detail?.WarehouseReceivedQuantity ?? header.WarehouseReceivedQuantity,
                    AcceptedQuantity = detail?.AcceptedQuantity ?? header.AcceptedQuantity,
                    PendingQcQuantity = detail?.PendingQcQuantity ?? header.PendingQcQuantity,
                    RejectedQuantity = detail?.RejectedQuantity ?? header.RejectedQuantity,
                    DelayDays = header.DeliveryDelayDays,
                    RequestDeliveryDate = header.RequestDeliveryDate,
                    ActualDate = detail?.DateWhenReceivedEnough ?? detail?.LastReceiptDate
                });
            }

            foreach (var detail in details)
            {
                if (detail.RejectedQuantity > 0)
                    reasons.Add(CreateRiskReason(detail, "HasQCFail", "Có hàng QC fail", 25, detail.RejectedQuantity, detail.LastReceiptDate));

                if (HasFlag(detail.AlertFlags, "Shortage"))
                    reasons.Add(CreateRiskReason(detail, "Shortage", "Thiếu hàng so với số lượng đặt", 20, detail.RemainingToReceive, detail.LastReceiptDate));

                if (detail.PendingQcQuantity > 0)
                    reasons.Add(CreateRiskReason(detail, "WaitingQC", "Có hàng nhập kho đang chờ QC", 10, detail.PendingQcQuantity, detail.LastReceiptDate));

                if (detail.OverReceivedQuantity > 0)
                    reasons.Add(CreateRiskReason(detail, "OverDelivered", "Giao vượt số lượng đặt vượt ngưỡng cho phép", 5, detail.OverReceivedQuantity, detail.LastReceiptDate));
            }

            return reasons
                .OrderByDescending(x => x.ImpactScore)
                .ThenByDescending(x => x.DelayDays)
                .ThenBy(x => x.POExternalId)
                .ThenBy(x => x.LineNo ?? int.MaxValue)
                .ToList();
        }

        private static PLPUSupplierRiskReasonDto CreateRiskReason(
            PLPUPurchaseOrderDetailReportDto detail,
            string reasonCode,
            string reasonText,
            decimal impactScore,
            decimal quantity,
            DateTime? actualDate)
        {
            return new PLPUSupplierRiskReasonDto
            {
                PurchaseOrderId = detail.PurchaseOrderId,
                POExternalId = detail.POExternalId,
                PurchaseOrderDetailId = detail.PurchaseOrderDetailId,
                LineNo = detail.LineNo,
                MaterialCode = detail.MaterialCode,
                MaterialName = detail.MaterialName,
                ReasonCode = reasonCode,
                ReasonText = reasonText,
                ImpactScore = impactScore,
                OrderedQuantity = detail.OrderedQuantity,
                WarehouseReceivedQuantity = detail.WarehouseReceivedQuantity,
                AcceptedQuantity = detail.AcceptedQuantity,
                PendingQcQuantity = detail.PendingQcQuantity,
                RejectedQuantity = detail.RejectedQuantity,
                DelayDays = detail.DeliveryDelayDays,
                RequestDeliveryDate = detail.RequestDeliveryDate,
                ActualDate = actualDate
            };
        }

}
