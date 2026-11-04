namespace HRM.Application.Features.Reports.PLPU;

public partial class PLPUPurchaseOverviewReportService
{
    private sealed record DrilldownMetric(
        Func<PLPUPurchaseOverviewSummaryDto, decimal> Value,
        Func<PLPUPurchaseOrderHeaderReportDto, bool>? OrderFilter = null,
        Func<PLPUPurchaseOrderDetailReportDto, decimal>? LineContribution = null);

    // PO predicates deliberately match BuildSummary, including legacy active/closed semantics.
    private static readonly IReadOnlyDictionary<string, DrilldownMetric> DrilldownMetrics =
        new Dictionary<string, DrilldownMetric>(StringComparer.Ordinal)
        {
            ["totalPo"] = new(s => s.TotalPO, _ => true),
            ["activePo"] = new(s => s.ActivePO, o => !IsCancelledOrInactiveStatus(o.POStatus)),
            ["cancelledPo"] = new(s => s.CancelledPO, o => o.ReportStatus == "Cancelled"),
            ["closedPo"] = new(s => s.ClosedPO, o => o.ReportStatus is "Closed" or "ClosedWithShortage"),
            ["completedPo"] = new(s => s.CompletedPO, o => o.ReportStatus == "Completed"),
            ["incompletePo"] = new(s => s.TotalPO - s.CompletedPO, o => o.ReportStatus != "Completed"),
            ["needActionPo"] = new(s => s.NeedActionPO, o => o.NeedBuyerAction),
            ["overduePo"] = new(s => s.OverduePO, o => o.IsOverdue),
            ["qcFailPo"] = new(s => s.QCFailPO, o => o.HasQCFail),
            ["pendingQcPo"] = new(s => s.PendingQCPO, o => o.HasPendingQC),
            ["overDeliveryPo"] = new(s => s.OverDeliveryPO, o => o.HasOverDelivery),
            ["orderedQuantity"] = new(s => s.OrderedQuantity, LineContribution: l => l.OrderedQuantity),
            ["warehouseReceivedQuantity"] = new(s => s.WarehouseReceivedQuantity, LineContribution: l => l.WarehouseReceivedQuantity),
            ["acceptedQuantity"] = new(s => s.AcceptedQuantity, LineContribution: l => l.AcceptedQuantity),
            ["pendingQcQuantity"] = new(s => s.PendingQcQuantity, LineContribution: l => l.PendingQcQuantity),
            ["rejectedQuantity"] = new(s => s.RejectedQuantity, LineContribution: l => l.RejectedQuantity),
            ["totalPurchaseValue"] = new(s => s.TotalPurchaseValue, LineContribution: l =>
                l.TotalPriceAgreed ?? l.OrderedQuantity * (l.UnitPriceAgreed ?? 0m)),
            ["receivedPurchaseValue"] = new(s => s.ReceivedPurchaseValue, LineContribution: l => l.ReceivedPurchaseValue)
        };

    public static bool IsDrilldownMetric(string? metric) => metric != null && DrilldownMetrics.ContainsKey(metric);

    public static string? ValidateReportStatus(string? metric, string? reportStatus)
    {
        if (reportStatus == null) return null;
        if (!PLPUReportStatuses.Labels.ContainsKey(reportStatus))
            return "Invalid ReportStatus. Use a computed PO report status code: " + string.Join(", ", PLPUReportStatuses.Labels.Keys);
        return metric == "totalPo" ? null : "ReportStatus is supported only with Metric=totalPo.";
    }

    public async Task<PLPUDrilldownDto> GetDrilldownAsync(
        PLPUPurchaseOverviewQuery query, string metric, CancellationToken ct = default, string? reportStatus = null)
    {
        if (!DrilldownMetrics.TryGetValue(metric, out var definition))
            throw new ArgumentException("Unsupported PLPU drill-down metric.", nameof(metric));
        var statusError = ValidateReportStatus(metric, reportStatus);
        if (statusError != null) throw new ArgumentException(statusError, nameof(reportStatus));

        var raw = await _repository.GetRawDataAsync(query, ct);
        var receipts = AllocateReceiptsToOrderLines(raw.OrderLines, raw.Receipts);
        var lines = BuildDetailRows(raw.OrderLines, receipts);
        var orders = BuildHeaderRows(raw.OrderLines, lines);
        if (reportStatus != null)
        {
            // Exactly the primary status grouped by /body, not overlapping alert flags.
            orders = orders.Where(o => o.ReportStatus == reportStatus).ToList();
            var orderIds = orders.Select(o => o.PurchaseOrderId).ToHashSet();
            lines = lines.Where(l => orderIds.Contains(l.PurchaseOrderId)).ToList();
        }
        var summary = BuildSummary(orders, lines);
        var rows = new List<PLPUDrilldownRowDto>();
        if (definition.OrderFilter is { } filter)
        {
            rows.AddRange(orders.Where(filter).Select(o => new PLPUDrilldownRowDto
            {
                Order = o, Contribution = 1m, ReportStatusCode = o.ReportStatus
            }));
        }
        else if (definition.LineContribution is { } contribution)
        {
            var orderById = orders.ToDictionary(o => o.PurchaseOrderId);
            rows.AddRange(lines.Select(l => new PLPUDrilldownRowDto
            {
                Order = orderById[l.PurchaseOrderId], Line = l, Contribution = contribution(l),
                ReportStatusCode = orderById[l.PurchaseOrderId].ReportStatus, LineStatusCode = l.LineReportStatus
            }).Where(r => r.Contribution != 0m));
        }

        // Stable tie-breaks prevent duplicated or skipped items between pages on the same dataset.
        var sorted = rows.OrderByDescending(r => r.Order.NeedBuyerAction)
            .ThenByDescending(r => r.Order.IsOverdue)
            .ThenBy(r => r.Order.RequestDeliveryDate ?? DateTime.MaxValue)
            .ThenByDescending(r => r.Order.CreatedDate)
            .ThenBy(r => r.Order.PurchaseOrderId)
            .ThenBy(r => r.Line?.LineNo ?? 0)
            .ThenBy(r => r.Line?.PurchaseOrderDetailId).ToList();
        var page = NormalizePageNumber(query.PageNumber);
        var size = NormalizePageSize(query.PageSize);
        var items = sorted.Skip((page - 1) * size).Take(size).ToList();

        // Localize each shared PO once, only after metric selection and totals are calculated.
        LocalizeHeaders(items.Select(r => r.Order).DistinctBy(o => o.PurchaseOrderId));
        LocalizeDetails(items.Where(r => r.Line != null).Select(r => r.Line!));
        return new PLPUDrilldownDto
        {
            CompanyId = query.CompanyId!.Value, Metric = metric,
            RowType = definition.OrderFilter != null ? "order" : "line",
            MetricValue = definition.Value(summary), PageValue = items.Sum(r => r.Contribution),
            TotalCount = rows.Count, Page = page, PageSize = size,
            TotalPages = (int)Math.Ceiling(rows.Count / (double)size), Items = items
        };
    }
}
