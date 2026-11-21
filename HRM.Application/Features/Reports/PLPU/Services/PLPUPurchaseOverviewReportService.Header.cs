namespace HRM.Application.Features.Reports.PLPU;
public partial class PLPUPurchaseOverviewReportService
{
    // One allocation pass for the selected summary and the six-month comparison.
    public async Task<(PLPUPurchaseOverviewSummaryDto Summary, IReadOnlyList<PLPUPurchaseReportChartPointDto> Monthly)>
        GetSummaryWithMonthlyAsync(Guid companyId, DateTime from, DateTime to, DateTime trendFrom, CancellationToken ct)
    {
        var raw = await _repository.GetRawDataAsync(new PLPUPurchaseOverviewQuery
        { CompanyId = companyId, From = from < trendFrom ? from : trendFrom, To = to }, ct);
        var receipts = AllocateReceiptsToOrderLines(raw.OrderLines, raw.Receipts);
        var details = BuildDetailRows(raw.OrderLines, receipts);
        var headers = BuildHeaderRows(raw.OrderLines, details);
        var selected = headers.Where(x => x.CreatedDate >= from && x.CreatedDate < to.Date.AddDays(1)).ToList();
        var ids = selected.Select(x => x.PurchaseOrderId).ToHashSet();
        return (BuildSummary(selected, details.Where(x => ids.Contains(x.PurchaseOrderId)).ToList()),
            BuildMonthChart(headers.Where(x => x.CreatedDate >= trendFrom).ToList()));
    }

    public async Task<PLPUPurchaseOverviewSummaryDto> GetSummaryAsync(PLPUPurchaseOverviewQuery query, CancellationToken ct = default)
    {
        var raw = await _repository.GetRawDataAsync(query, ct);
        var receipts = AllocateReceiptsToOrderLines(raw.OrderLines, raw.Receipts);
        var details = BuildDetailRows(raw.OrderLines, receipts);
        return BuildSummary(BuildHeaderRows(raw.OrderLines, details), details);
    }
}
