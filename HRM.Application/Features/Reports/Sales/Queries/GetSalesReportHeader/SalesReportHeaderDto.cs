namespace HRM.Application.Features.Reports.Sales.Queries.GetSalesReportHeader;

public sealed class SalesReportHeaderDto
{
    public Guid CompanyId { get; init; }
    public DateTime From { get; init; }
    public DateTime To { get; init; }
    public DateTime PreviousFrom { get; init; }
    public DateTime PreviousTo { get; init; }
    public string Currency { get; init; } = "VND";
    public decimal Revenue { get; init; }
    public decimal ExpectedPendingAmountInPeriod { get; init; }
    public IReadOnlyList<SalesReportExpectedPendingMonthDto> ExpectedPendingByMonth { get; init; } = [];
    public decimal ExpectedRevenueInPeriod { get; init; }
    public decimal PreviousRevenue { get; init; }
    public decimal RevenueGrowthAmount { get; init; }
    public decimal? RevenueGrowthPercent { get; init; }
    public decimal DeliveredQuantity { get; init; }
    public decimal? AverageSellingPrice { get; init; }
    public int CustomerCount { get; init; }
    public int DeliveredOrderCount { get; init; }
    public int NewOrderCount { get; init; }
    public int MerchandisePoCount { get; init; }
    public int ComplaintPoCount { get; init; }
    public int SamplePoCount { get; init; }
    public int CompletedPoCount { get; init; }
    public int IncompletePoCount { get; init; }
    public IReadOnlyList<SalesReportMonthlyRevenueDto> MonthlyRevenue { get; init; } = [];
    public decimal NewOrderAmount { get; init; }
    public decimal PendingQuantity { get; init; }
    public decimal PendingAmount { get; init; }
    public int PendingOrderCount { get; init; }
    public int OverdueOrderCount { get; init; }
    public bool CanViewProfit { get; init; }
    public decimal? CostOfSales { get; init; }
    public decimal? Profit { get; init; }
    public decimal? MarginPercent { get; init; }
}

public sealed record SalesReportExpectedPendingMonthDto(DateTime Month, decimal Amount);
