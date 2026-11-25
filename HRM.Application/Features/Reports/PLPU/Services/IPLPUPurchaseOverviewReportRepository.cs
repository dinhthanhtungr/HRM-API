
namespace HRM.Application.Features.Reports.PLPU
{
    public interface IPLPUPurchaseOverviewReportRepository
    {
        Task<PLPUPurchaseReportRawData> GetRawDataAsync(PLPUPurchaseOverviewQuery query, CancellationToken ct);
        Task<PLPUMaterialActivityDto?> GetMaterialActivityAsync(PLPUPurchaseOverviewQuery query, CancellationToken ct)
            => Task.FromResult<PLPUMaterialActivityDto?>(null);
    }
}
