using HRM.Application.Commons.Reporting.SalesMetrics;
using HRM.Application.Features.Reports.Sales.Queries.GetSalesReportHeader;

namespace HRM.Application.Features.Reports.Sales.Queries.GetSalesReportBody;

internal sealed record SalesReportBodyLine(string GroupKey, string GroupLabel, string Key, string Label,
    decimal Revenue, decimal Quantity, decimal? Cost);

internal static class SalesReportBodyBuilder
{
    public static SalesReportBodyDto Build(Guid companyId, SalesReportPeriod period,
        SalesReportDimension dimension, bool canViewProfit, IReadOnlyList<SalesReportBodyLine> rows)
        => new()
        {
            CompanyId = companyId, From = period.From, To = period.To, GroupBy = dimension,
            CanViewProfit = canViewProfit, Totals = Metrics(rows, canViewProfit),
            Groups = rows.GroupBy(x => new { x.GroupKey, x.GroupLabel })
                .Select(g => new SalesReportBodyGroupDto(g.Key.GroupKey, g.Key.GroupLabel,
                    Metrics(g, canViewProfit), g.GroupBy(x => new { x.Key, x.Label })
                        .Select(r => new SalesReportBodyRowDto(r.Key.Key, r.Key.Label, Metrics(r, canViewProfit)))
                        .OrderByDescending(r => r.Metrics.Revenue).ThenBy(r => r.Key).ToList()))
                .OrderByDescending(g => g.Metrics.Revenue).ThenBy(g => g.Key).ToList()
        };

    private static SalesReportBodyMetricsDto Metrics(IEnumerable<SalesReportBodyLine> source, bool canViewProfit)
    {
        var rows = source.ToList();
        var revenue = rows.Sum(x => x.Revenue);
        decimal? cost = canViewProfit ? rows.Sum(x => x.Cost ?? 0m) : null;
        var profit = revenue - cost;
        return new() { Revenue = revenue, DeliveredQuantity = rows.Sum(x => x.Quantity),
            CostOfSales = cost, Profit = profit,
            MarginPercent = profit.HasValue ? ReportMetricRules.MarginPercent(revenue, profit.Value) : null };
    }
}
