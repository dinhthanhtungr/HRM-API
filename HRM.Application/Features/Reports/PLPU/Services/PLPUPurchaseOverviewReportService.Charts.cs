using HRM.Domain.Enums.Devandqa;
namespace HRM.Application.Features.Reports.PLPU;

public partial class PLPUPurchaseOverviewReportService
{
        private static IReadOnlyList<PLPUPurchaseReportChartPointDto> BuildMonthChart(
            List<PLPUPurchaseOrderHeaderReportDto> headers)
        {
            return headers
                .Where(x => x.CreatedDate.HasValue)
                .GroupBy(x => new { x.CreatedDate!.Value.Year, x.CreatedDate!.Value.Month })
                .OrderBy(x => x.Key.Year)
                .ThenBy(x => x.Key.Month)
                .Select(g =>
                {
                    var start = new DateTime(g.Key.Year, g.Key.Month, 1);
                    var ordered = g.Sum(x => x.OrderedQuantity);
                    var received = g.Sum(x => x.WarehouseReceivedQuantity);
                    var accepted = g.Sum(x => x.AcceptedQuantity);

                    return new PLPUPurchaseReportChartPointDto
                    {
                        Label = start.ToString("yyyy-MM"),
                        PurchaseOrderCount = g.Count(),
                        OrderedQuantity = ordered,
                        WarehouseReceivedQuantity = received,
                        AcceptedQuantity = accepted,
                        PendingQcQuantity = g.Sum(x => x.PendingQcQuantity),
                        RejectedQuantity = g.Sum(x => x.RejectedQuantity),
                        TotalPurchaseValue = g.Sum(x => x.TotalPurchaseValue),
                        ReceivedPurchaseValue = g.Sum(x => x.ReceivedPurchaseValue),
                        CompletionPercent = Percent(accepted, ordered),
                        WarehouseReceiptPercent = Percent(received, ordered),
                        PeriodStart = start,
                        PeriodEnd = start.AddMonths(1).AddDays(-1)
                    };
                })
                .ToList();
        }

        /// <summary>
        /// Tạo biểu đồ số lượng mua hàng theo tháng.
        /// </summary>
        private static IReadOnlyList<PLPUPurchaseReportChartPointDto> BuildMonthQuantityChart(
            List<PLPUPurchaseOrderHeaderReportDto> headers)
        {
            return BuildMonthChart(headers)
                .Select(x =>
                {
                    x.TotalPurchaseValue = 0;
                    return x;
                })
                .ToList();
        }

        /// <summary>
        /// Tạo biểu đồ giá trị mua hàng theo nhà cung cấp.
        /// </summary>
        private static IReadOnlyList<PLPUPurchaseReportChartPointDto> BuildSupplierChart(
            List<PLPUPurchaseOrderHeaderReportDto> headers,
            List<PLPUPurchaseOrderDetailReportDto> details)
        {
            var supplierByPo = headers.ToDictionary(x => x.PurchaseOrderId, x => x.SupplierId);
            var detailsBySupplier = details
                .GroupBy(x => supplierByPo.TryGetValue(x.PurchaseOrderId, out var supplierId)
                    ? supplierId ?? Guid.Empty
                    : Guid.Empty)
                .ToDictionary(x => x.Key, x => x.ToList());

            return headers
                .GroupBy(x => new { x.SupplierId, x.SupplierName })
                .OrderByDescending(x => x.Sum(r => r.TotalPurchaseValue))
                .Take(15)
                .Select(g =>
                {
                    var ordered = g.Sum(x => x.OrderedQuantity);
                    var received = g.Sum(x => x.WarehouseReceivedQuantity);
                    var accepted = g.Sum(x => x.AcceptedQuantity);
                    var supplierKey = g.Key.SupplierId ?? Guid.Empty;
                    var supplierDetails = detailsBySupplier.TryGetValue(supplierKey, out var supplierDetailRows)
                        ? supplierDetailRows
                        : new List<PLPUPurchaseOrderDetailReportDto>();
                    var deliveryEvaluable = supplierDetails.Where(IsDeliveryOtdEvaluable).ToList();
                    var acceptanceEvaluable = supplierDetails.Where(IsAcceptanceOtdEvaluable).ToList();

                    return new PLPUPurchaseReportChartPointDto
                    {
                        Id = g.Key.SupplierId,
                        Label = string.IsNullOrWhiteSpace(g.Key.SupplierName)
                            ? "Unknown supplier"
                            : g.Key.SupplierName,
                        PurchaseOrderCount = g.Count(),
                        OrderedQuantity = ordered,
                        WarehouseReceivedQuantity = received,
                        AcceptedQuantity = accepted,
                        PendingQcQuantity = g.Sum(x => x.PendingQcQuantity),
                        RejectedQuantity = g.Sum(x => x.RejectedQuantity),
                        TotalPurchaseValue = g.Sum(x => x.TotalPurchaseValue),
                        ReceivedPurchaseValue = g.Sum(x => x.ReceivedPurchaseValue),
                        CompletionPercent = Percent(accepted, ordered),
                        WarehouseReceiptPercent = Percent(received, ordered),
                        QcFailRate = Percent(g.Sum(x => x.RejectedQuantity), received),
                        QcAcceptanceRate = Percent(accepted, received),
                        AverageDelayDays = deliveryEvaluable.Count > 0
                            ? Math.Round((decimal)deliveryEvaluable.Average(x => x.DeliveryDelayDays), 2)
                            : 0,
                        AverageDeliveryDelayDays = deliveryEvaluable.Count > 0
                            ? Math.Round((decimal)deliveryEvaluable.Average(x => x.DeliveryDelayDays), 2)
                            : 0,
                        AverageAcceptanceDelayDays = acceptanceEvaluable.Count > 0
                            ? Math.Round((decimal)acceptanceEvaluable.Average(x => x.AcceptanceDelayDays), 2)
                            : 0
                    };
                })
                .ToList();
        }

        /// <summary>
        /// Tạo biểu đồ số lượng mua hàng theo nhà cung cấp.
        /// </summary>
        private static IReadOnlyList<PLPUPurchaseReportChartPointDto> BuildSupplierQuantityChart(
            List<PLPUPurchaseOrderHeaderReportDto> headers,
            List<PLPUPurchaseOrderDetailReportDto> details)
        {
            return BuildSupplierChart(headers, details)
                .OrderByDescending(x => x.OrderedQuantity)
                .ToList();
        }

        /// <summary>
        /// Tạo biểu đồ QC theo nhà cung cấp.
        /// </summary>
        private static IReadOnlyList<PLPUPurchaseReportChartPointDto> BuildSupplierQcChart(
            List<PLPUPurchaseOrderHeaderReportDto> headers,
            List<PLPUPurchaseOrderDetailReportDto> details)
        {
            return BuildSupplierChart(headers, details)
                .Where(x => x.WarehouseReceivedQuantity > 0 ||
                            x.PendingQcQuantity > 0 ||
                            x.RejectedQuantity > 0)
                .OrderByDescending(x => x.QcFailRate)
                .ThenByDescending(x => x.RejectedQuantity)
                .ToList();
        }

        /// <summary>
        /// Tạo biểu đồ phân bố trạng thái PO.
        /// </summary>
        private static IReadOnlyList<PLPUPurchaseReportChartPointDto> BuildStatusChart(
            List<PLPUPurchaseOrderHeaderReportDto> headers)
        {
            return headers
                .GroupBy(x => x.ReportStatus)
                .OrderByDescending(x => x.Count())
                .Select(g =>
                {
                    var ordered = g.Sum(x => x.OrderedQuantity);
                    var received = g.Sum(x => x.WarehouseReceivedQuantity);
                    var accepted = g.Sum(x => x.AcceptedQuantity);

                    return new PLPUPurchaseReportChartPointDto
                    {
                        Label = ToVietnameseStatus(g.Key),
                        PurchaseOrderCount = g.Count(),
                        OrderedQuantity = ordered,
                        WarehouseReceivedQuantity = received,
                        AcceptedQuantity = accepted,
                        PendingQcQuantity = g.Sum(x => x.PendingQcQuantity),
                        RejectedQuantity = g.Sum(x => x.RejectedQuantity),
                        TotalPurchaseValue = g.Sum(x => x.TotalPurchaseValue),
                        ReceivedPurchaseValue = g.Sum(x => x.ReceivedPurchaseValue),
                        CompletionPercent = Percent(accepted, ordered),
                        WarehouseReceiptPercent = Percent(received, ordered)
                    };
                })
                .ToList();
        }

        /// <summary>
        /// Tạo biểu đồ số lượng mua theo vật tư.
        /// </summary>
        private static IReadOnlyList<PLPUPurchaseReportChartPointDto> BuildMaterialQuantityChart(
            List<PLPUPurchaseOrderDetailReportDto> details,
            Dictionary<Guid, PLPUPurchaseOrderLineRaw> linesByDetailId)
        {
            return details
                .Where(x => linesByDetailId.ContainsKey(x.PurchaseOrderDetailId))
                .GroupBy(x => new { x.MaterialId, x.MaterialCode, x.MaterialName })
                .OrderByDescending(x => x.Sum(r => r.OrderedQuantity))
                .Take(20)
                .Select(g =>
                {
                    var ordered = g.Sum(x => x.OrderedQuantity);
                    var received = g.Sum(x => x.WarehouseReceivedQuantity);
                    var accepted = g.Sum(x => x.AcceptedQuantity);

                    return new PLPUPurchaseReportChartPointDto
                    {
                        Id = g.Key.MaterialId,
                        Label = string.IsNullOrWhiteSpace(g.Key.MaterialCode)
                            ? g.Key.MaterialName
                            : $"{g.Key.MaterialCode} - {g.Key.MaterialName}",
                        LineCount = g.Count(),
                        PurchaseOrderCount = g.Select(x => x.PurchaseOrderId).Distinct().Count(),
                        OrderedQuantity = ordered,
                        WarehouseReceivedQuantity = received,
                        AcceptedQuantity = accepted,
                        PendingQcQuantity = g.Sum(x => x.PendingQcQuantity),
                        RejectedQuantity = g.Sum(x => x.RejectedQuantity),
                        TotalPurchaseValue = g.Sum(x =>
                            x.TotalPriceAgreed ??
                            x.OrderedQuantity * (x.UnitPriceAgreed ?? 0)),
                        ReceivedPurchaseValue = g.Sum(x => x.ReceivedPurchaseValue),
                        CompletionPercent = Percent(accepted, ordered),
                        WarehouseReceiptPercent = Percent(received, ordered),
                        QcFailRate = Percent(g.Sum(x => x.RejectedQuantity), received),
                        QcAcceptanceRate = Percent(accepted, received)
                    };
                })
                .ToList();
        }

        /// <summary>
        /// Tạo biểu đồ chênh lệch giá theo vật tư.
        /// </summary>
        private static IReadOnlyList<PLPUPurchaseReportChartPointDto> BuildMaterialPriceVarianceChart(
            List<PLPUPurchaseOrderDetailReportDto> details,
            Dictionary<Guid, PLPUPurchaseOrderLineRaw> linesByDetailId)
        {
            return details
                .Where(x => linesByDetailId.ContainsKey(x.PurchaseOrderDetailId) && x.PriceVariance.HasValue)
                .GroupBy(x => new { x.MaterialId, x.MaterialCode, x.MaterialName })
                .Select(g => new PLPUPurchaseReportChartPointDto
                {
                    Id = g.Key.MaterialId,
                    Label = string.IsNullOrWhiteSpace(g.Key.MaterialCode)
                        ? g.Key.MaterialName
                        : $"{g.Key.MaterialCode} - {g.Key.MaterialName}",
                    LineCount = g.Count(),
                    OrderedQuantity = g.Sum(x => x.OrderedQuantity),
                    TotalPurchaseValue = g.Sum(x =>
                        x.TotalPriceAgreed ??
                        x.OrderedQuantity * (x.UnitPriceAgreed ?? 0)),
                    ReceivedPurchaseValue = g.Sum(x => x.ReceivedPurchaseValue),
                    PriceVarianceAmount = g.Sum(x => x.PriceImpactAmount ?? 0)
                })
                .OrderByDescending(x => Math.Abs(x.PriceVarianceAmount))
                .Take(20)
                .ToList();
        }

        // ========================================== Các hàm logic nhỏ ===========================================

}
