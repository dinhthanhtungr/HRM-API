using HRM.Domain.Enums.Devandqa;
using HRM.Domain.Enums.WareHouses;
namespace HRM.Application.Features.Reports.PLPU;

    public class PLPUPurchaseOverviewReportDto
    {
        public PLPUPurchaseOverviewSummaryDto Summary { get; set; } = new();

        public PLPUPagedResult<PLPUPurchaseOrderHeaderReportDto> PurchaseOrders { get; set; } =
            new(new List<PLPUPurchaseOrderHeaderReportDto>(), 0, 1, 15);

        public List<PLPUPurchaseOrderHeaderReportDto> NeedActionPurchaseOrders { get; set; } = new();

        public List<PLPUSupplierPurchasePerformanceDto> SupplierPerformance { get; set; } = new();
    }
