namespace HRM.Application.Commons.Reporting.SalesMetrics;

internal static class ReportMetricRules
{
    public static decimal MarginPercent(decimal revenue, decimal profit)
        => revenue == 0m ? 0m : Math.Round(profit / revenue * 100m, 2);

    public static decimal? GrowthPercent(decimal current, decimal previous)
        => previous == 0m ? null : Math.Round((current - previous) / previous * 100m, 2);

    public static decimal ExchangeRate(string? currency, decimal? exchangeRate)
        => string.IsNullOrWhiteSpace(currency) || currency.ToUpper() == "VND"
            || !exchangeRate.HasValue || exchangeRate.Value <= 0m ? 1m : exchangeRate.Value;
}
