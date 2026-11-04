using HRM.Domain.Enums.Devandqa;
using HRM.Domain.Enums.WareHouses;
namespace HRM.Application.Features.Reports.PLPU;

    public class PLPUPurchaseOrderDetailDashboardDto
    {
        public PLPUPurchaseOrderHeaderReportDto Header { get; set; } = new();

        public IReadOnlyList<PLPUPurchaseOrderDetailReportDto> Lines { get; set; } =
            new List<PLPUPurchaseOrderDetailReportDto>();

        public IReadOnlyList<PLPUPurchaseReceiptReportDto> Receipts { get; set; } =
            new List<PLPUPurchaseReceiptReportDto>();

        public IReadOnlyList<PLPUPurchaseQcReportDto> QcRows { get; set; } =
            new List<PLPUPurchaseQcReportDto>();
    }
