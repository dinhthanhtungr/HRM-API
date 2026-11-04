namespace HRM.Application.Features.Reports.PLPU;
public partial class PLPUPurchaseOverviewReportService
{
    public async Task<PLPUPurchaseOverviewSummaryDto> GetSummaryAsync(PLPUPurchaseOverviewQuery query, CancellationToken ct = default)
    {
        var raw = await _repository.GetRawDataAsync(query, ct);
        var receipts = AllocateReceiptsToOrderLines(raw.OrderLines, raw.Receipts);
        var details = BuildDetailRows(raw.OrderLines, receipts);
        return BuildSummary(BuildHeaderRows(raw.OrderLines, details), details);
    }
}
