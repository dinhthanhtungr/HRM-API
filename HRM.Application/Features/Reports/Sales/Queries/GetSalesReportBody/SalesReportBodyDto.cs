namespace HRM.Application.Features.Reports.Sales.Queries.GetSalesReportBody;

public sealed class SalesReportBodyDto
{
    public Guid CompanyId { get; init; }
    public DateTime From { get; init; }
    public DateTime To { get; init; }
    public string Currency { get; init; } = "VND";
    public SalesReportDimension GroupBy { get; init; }
    public bool CanViewProfit { get; init; }
    public SalesReportBodyMetricsDto Totals { get; init; } = new();
    public IReadOnlyList<SalesReportBodyGroupDto> Groups { get; init; } = [];
}

public sealed class SalesReportBodyMetricsDto
{
    public decimal Revenue { get; init; }
    public decimal DeliveredQuantity { get; init; }
    public decimal? CostOfSales { get; init; }
    public decimal? Profit { get; init; }
    public decimal? MarginPercent { get; init; }
}

public sealed record SalesReportBodyRowDto(string Key, string Label, SalesReportBodyMetricsDto Metrics);
public sealed record SalesReportBodyGroupDto(string Key, string Label, SalesReportBodyMetricsDto Metrics,
    IReadOnlyList<SalesReportBodyRowDto> Rows);
