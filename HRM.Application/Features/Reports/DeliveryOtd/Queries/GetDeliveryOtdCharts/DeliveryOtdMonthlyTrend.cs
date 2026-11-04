namespace HRM.Application.Features.Reports.DeliveryOtd.Queries.GetDeliveryOtdCharts;

internal static class DeliveryOtdMonthlyTrend
{
    internal static DeliveryOtdFilter Expand(DeliveryOtdFilter source)
    {
        var end = new DateTime(source.To.Year, source.To.Month, DateTime.DaysInMonth(source.To.Year, source.To.Month));
        var start = new DateTime(source.From.Year, source.From.Month, 1);
        var monthCount = (end.Year - start.Year) * 12 + end.Month - start.Month + 1;
        if (monthCount < 6)
        {
            var lastMonth = new DateTime(end.Year, end.Month, 1);
            start = lastMonth.Year == 1 && lastMonth.Month < 6 ? DateTime.MinValue.Date : lastMonth.AddMonths(-5);
        }
        return new DeliveryOtdFilter
        {
            From = start, To = end, AsOf = source.AsOf, DateBasis = source.DateBasis,
            CustomerId = source.CustomerId, ProductId = source.ProductId, ManagerId = source.ManagerId
        };
    }

    internal static IReadOnlyList<DeliveryOtdChartPoint> Fill(IReadOnlyList<DeliveryOtdChartPoint> points,
        DeliveryOtdFilter window, DateTime asOf)
    {
        var lookup = points.ToDictionary(x => x.Key);
        var result = new List<DeliveryOtdChartPoint>();
        for (var month = window.From; month <= window.To;)
        {
            var key = month.ToString("yyyy-MM");
            result.Add(lookup.TryGetValue(key, out var point) ? point
                : new(key, key, DeliveryOtdCalculator.Summarize([], asOf)));
            if (month.Year == window.To.Year && month.Month == window.To.Month) break;
            month = month.AddMonths(1);
        }
        return result;
    }
}
