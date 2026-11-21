namespace HRM.Application.Features.Reports.PLPU;

public partial class PLPUPurchaseOverviewReportService
{
    // Same allocation, prices and PO cohort as the dashboard. Never apply UI pagination to an export.
    public async Task<IReadOnlyList<PLPUPurchaseOrderDetailReportDto>> GetExportLinesAsync(
        PLPUPurchaseOverviewQuery query, CancellationToken ct = default)
    {
        var raw = await _repository.GetRawDataAsync(query, ct);
        var receipts = AllocateReceiptsToOrderLines(raw.OrderLines, raw.Receipts);
        return LocalizeDetails(BuildDetailRows(raw.OrderLines, receipts))
            .OrderBy(x => x.POExternalId).ThenBy(x => x.LineNo).ThenBy(x => x.PurchaseOrderDetailId).ToArray();
    }
}
