using HRM.Domain.Enums.Devandqa;
namespace HRM.Application.Features.Reports.PLPU;

public partial class PLPUPurchaseOverviewReportService
{
        private static PLPUPurchaseOverviewSummaryDto BuildSummary(
            List<PLPUPurchaseOrderHeaderReportDto> headers,
            List<PLPUPurchaseOrderDetailReportDto> details)
        {
            var ordered = details.Sum(x => x.OrderedQuantity);
            var received = details.Sum(x => x.WarehouseReceivedQuantity);
            var accepted = details.Sum(x => x.AcceptedQuantity);
            var pendingQc = details.Sum(x => x.PendingQcQuantity);
            var rejected = details.Sum(x => x.RejectedQuantity);

            var deliveryEvaluableDetails = details
                .Where(IsDeliveryOtdEvaluable)
                .ToList();

            var acceptanceEvaluableDetails = details
                .Where(IsAcceptanceOtdEvaluable)
                .ToList();

            return new PLPUPurchaseOverviewSummaryDto
            {
                TotalPO = headers.Count,
                ActivePO = headers.Count(x => !IsCancelledOrInactiveStatus(x.POStatus)),
                CancelledPO = headers.Count(x => x.ReportStatus == "Cancelled"),
                ClosedPO = headers.Count(x => x.ReportStatus == "Closed" || x.ReportStatus == "ClosedWithShortage"),
                CompletedPO = headers.Count(x => x.ReportStatus == "Completed"),

                NeedActionPO = headers.Count(x => x.NeedBuyerAction),
                OverduePO = headers.Count(x => x.IsOverdue),
                QCFailPO = headers.Count(x => x.HasQCFail),
                PendingQCPO = headers.Count(x => x.HasPendingQC),
                OverDeliveryPO = headers.Count(x => x.HasOverDelivery),

                TotalPurchaseValue = headers.Sum(x => x.TotalPurchaseValue),
                ReceivedPurchaseValue = headers.Sum(x => x.ReceivedPurchaseValue),

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

                OnTimeDeliveryRate = Percent(
                    deliveryEvaluableDetails.Count(IsDeliveryOnTime),
                    deliveryEvaluableDetails.Count),

                OnTimeAcceptanceRate = Percent(
                    acceptanceEvaluableDetails.Count(IsAcceptanceOnTime),
                    acceptanceEvaluableDetails.Count),

                ShortageRate = Percent(
                    details.Where(x => HasFlag(x.AlertFlags, "Shortage")).Sum(x => x.RemainingToAccept),
                    ordered),

                OverDeliveryRate = Percent(details.Sum(x => x.OverReceivedQuantity), ordered),

                QcFailRate = Percent(rejected, received),
                QcAcceptanceRate = Percent(accepted, received),
                PendingQcRate = Percent(pendingQc, received),

                AverageDeliveryDelayDays = deliveryEvaluableDetails.Count > 0
                    ? Math.Round((decimal)deliveryEvaluableDetails.Average(x => x.DeliveryDelayDays), 2)
                    : 0,

                AverageAcceptanceDelayDays = acceptanceEvaluableDetails.Count > 0
                    ? Math.Round((decimal)acceptanceEvaluableDetails.Average(x => x.AcceptanceDelayDays), 2)
                    : 0,

                AverageDelayDays = deliveryEvaluableDetails.Count > 0
                    ? Math.Round((decimal)deliveryEvaluableDetails.Average(x => x.DeliveryDelayDays), 2)
                    : 0,

                MaxDeliveryDelayDays = details.Count > 0 ? details.Max(x => x.DeliveryDelayDays) : 0,
                MaxAcceptanceDelayDays = details.Count > 0 ? details.Max(x => x.AcceptanceDelayDays) : 0,
                MaxDelayDays = details.Count > 0 ? details.Max(x => x.DelayDays) : 0
            };
        }

        /// <summary>
        /// Tạo dữ liệu dashboard header gồm summary và các biểu đồ.
        /// </summary>
        private static PLPUPurchaseOrderHeaderDashboardDto BuildHeaderDashboard(
            List<PLPUPurchaseOrderLineRaw> orderLines,
            List<PLPUPurchaseOrderDetailReportDto> details,
            List<PLPUPurchaseOrderHeaderReportDto> headers)
        {
            var summary = BuildSummary(headers, details);
            var linesByDetailId = orderLines.ToDictionary(x => x.PurchaseOrderDetailId, x => x);

            return new PLPUPurchaseOrderHeaderDashboardDto
            {
                TotalPO = summary.TotalPO,
                ActivePO = summary.ActivePO,
                CancelledPO = summary.CancelledPO,
                ClosedPO = summary.ClosedPO,
                CompletedPO = summary.CompletedPO,
                NeedActionPO = summary.NeedActionPO,
                OverduePO = summary.OverduePO,
                QCFailPO = summary.QCFailPO,
                PendingQCPO = summary.PendingQCPO,
                OverDeliveryPO = summary.OverDeliveryPO,

                TotalPurchaseValue = summary.TotalPurchaseValue,
                ReceivedPurchaseValue = summary.ReceivedPurchaseValue,

                OrderedQuantity = summary.OrderedQuantity,
                WarehouseReceivedQuantity = summary.WarehouseReceivedQuantity,
                AcceptedQuantity = summary.AcceptedQuantity,
                PendingQcQuantity = summary.PendingQcQuantity,
                RejectedQuantity = summary.RejectedQuantity,

                RemainingToReceive = summary.RemainingToReceive,
                RemainingToAccept = summary.RemainingToAccept,
                OverReceivedQuantity = summary.OverReceivedQuantity,

                CompletionPercent = summary.CompletionPercent,
                WarehouseReceiptPercent = summary.WarehouseReceiptPercent,

                OnTimeDeliveryRate = summary.OnTimeDeliveryRate,
                OnTimeAcceptanceRate = summary.OnTimeAcceptanceRate,

                ShortageRate = summary.ShortageRate,
                OverDeliveryRate = summary.OverDeliveryRate,
                QcFailRate = summary.QcFailRate,
                QcAcceptanceRate = summary.QcAcceptanceRate,
                PendingQcRate = summary.PendingQcRate,

                AverageDeliveryDelayDays = summary.AverageDeliveryDelayDays,
                AverageAcceptanceDelayDays = summary.AverageAcceptanceDelayDays,
                AverageDelayDays = summary.AverageDelayDays,

                MaxDeliveryDelayDays = summary.MaxDeliveryDelayDays,
                MaxAcceptanceDelayDays = summary.MaxAcceptanceDelayDays,
                MaxDelayDays = summary.MaxDelayDays,

                PurchaseValueByMonth = BuildMonthChart(headers),
                QuantityByMonth = BuildMonthQuantityChart(headers),
                PurchaseValueBySupplier = BuildSupplierChart(headers, details),
                QuantityBySupplier = BuildSupplierQuantityChart(headers, details),
                QuantityByMaterial = BuildMaterialQuantityChart(details, linesByDetailId),
                QcResultBySupplier = BuildSupplierQcChart(headers, details),
                StatusDistribution = BuildStatusChart(headers),
                PriceVarianceByMaterial = BuildMaterialPriceVarianceChart(details, linesByDetailId)
            };
        }

        // ========================================== Các hàm chart ===========================================

}
