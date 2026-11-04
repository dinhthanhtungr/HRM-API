
namespace HRM.Application.Features.Reports.PLPU
{
    public interface IPLPUPurchaseOverviewReportRepository
    {
        Task<PLPUPurchaseReportRawData> GetRawDataAsync(PLPUPurchaseOverviewQuery query, CancellationToken ct);
    }
}
