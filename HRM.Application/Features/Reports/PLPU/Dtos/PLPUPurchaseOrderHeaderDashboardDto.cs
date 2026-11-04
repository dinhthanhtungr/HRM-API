using HRM.Domain.Enums.Devandqa;
using HRM.Domain.Enums.WareHouses;
namespace HRM.Application.Features.Reports.PLPU;

    public class PLPUPurchaseOrderHeaderDashboardDto : PLPUPurchaseOverviewSummaryDto
    {
        public IReadOnlyList<PLPUPurchaseReportChartPointDto> PurchaseValueByMonth { get; set; } =
            new List<PLPUPurchaseReportChartPointDto>();

        public IReadOnlyList<PLPUPurchaseReportChartPointDto> QuantityByMonth { get; set; } =
            new List<PLPUPurchaseReportChartPointDto>();

        public IReadOnlyList<PLPUPurchaseReportChartPointDto> PurchaseValueBySupplier { get; set; } =
            new List<PLPUPurchaseReportChartPointDto>();

        public IReadOnlyList<PLPUPurchaseReportChartPointDto> QuantityBySupplier { get; set; } =
            new List<PLPUPurchaseReportChartPointDto>();

        public IReadOnlyList<PLPUPurchaseReportChartPointDto> QuantityByMaterial { get; set; } =
            new List<PLPUPurchaseReportChartPointDto>();

        public IReadOnlyList<PLPUPurchaseReportChartPointDto> QcResultBySupplier { get; set; } =
            new List<PLPUPurchaseReportChartPointDto>();

        public IReadOnlyList<PLPUPurchaseReportChartPointDto> StatusDistribution { get; set; } =
            new List<PLPUPurchaseReportChartPointDto>();

        public IReadOnlyList<PLPUPurchaseReportChartPointDto> PriceVarianceByMaterial { get; set; } =
            new List<PLPUPurchaseReportChartPointDto>();
    }
