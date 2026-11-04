namespace HRM.Application.Features.Reports.PLPU;
public sealed class PLPUPurchaseBodyDto
{
    public IReadOnlyList<PLPUPurchaseReportChartPointDto> PurchaseValueByMonth { get; init; } = [];
    public IReadOnlyList<PLPUPurchaseReportChartPointDto> QuantityByMonth { get; init; } = [];
    public IReadOnlyList<PLPUPurchaseReportChartPointDto> PurchaseValueBySupplier { get; init; } = [];
    public IReadOnlyList<PLPUPurchaseReportChartPointDto> QuantityBySupplier { get; init; } = [];
    public IReadOnlyList<PLPUPurchaseReportChartPointDto> QuantityByMaterial { get; init; } = [];
    public IReadOnlyList<PLPUPurchaseReportChartPointDto> QcResultBySupplier { get; init; } = [];
    public IReadOnlyList<PLPUPurchaseReportChartPointDto> StatusDistribution { get; init; } = [];
    public IReadOnlyList<PLPUPurchaseReportChartPointDto> PriceVarianceByMaterial { get; init; } = [];
}
