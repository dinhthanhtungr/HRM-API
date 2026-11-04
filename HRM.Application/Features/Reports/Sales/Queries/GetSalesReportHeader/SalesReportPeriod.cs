namespace HRM.Application.Features.Reports.Sales.Queries.GetSalesReportHeader;

internal sealed record SalesReportPeriod(DateTime From, DateTime ToExclusive)
{
    public DateTime To => ToExclusive.AddDays(-1);
    public DateTime PreviousFrom => From.AddDays(-(ToExclusive - From).Days);
    public DateTime PreviousTo => From.AddDays(-1);

    public static SalesReportPeriod? Create(DateTime? from, DateTime? to, DateTime today)
    {
        var end = to?.Date ?? today.Date;
        var start = from?.Date ?? new DateTime(end.Year, end.Month, 1);
        if (end == DateTime.MaxValue.Date || start > end || (end - start).Days > 1095
            || start.Ticks < TimeSpan.FromDays((end - start).Days + 1).Ticks)
            return null;
        return new(start, end.AddDays(1));
    }
}
