namespace HRM.Application.Features.Reports.PLPU.Queries.GetPLPUBody;

internal static class PLPUMonthChartWindow
{
    private const int MinimumMonths = 6;

    internal static (DateTime From, DateTime To) Resolve(PLPUPurchaseOverviewQuery query, DateTime today)
    {
        var anchor = query.To ?? today;
        var lastMonth = new DateTime(anchor.Year, anchor.Month, 1);
        var firstMonth = lastMonth.Year == 1 && lastMonth.Month < MinimumMonths
            ? DateTime.MinValue.Date : lastMonth.AddMonths(1 - MinimumMonths);
        if (query.From.HasValue)
        {
            var selected = new DateTime(query.From.Value.Year, query.From.Value.Month, 1);
            if (selected < firstMonth) firstMonth = selected;
        }
        return (firstMonth, new DateTime(anchor.Year, anchor.Month, DateTime.DaysInMonth(anchor.Year, anchor.Month)));
    }

    internal static PLPUPurchaseOverviewQuery CreateQuery(PLPUPurchaseOverviewQuery source, DateTime from, DateTime to)
        => new()
        {
            From = from, To = to, CompanyId = source.CompanyId, PurchaseOrderId = source.PurchaseOrderId,
            SupplierId = source.SupplierId, MaterialId = source.MaterialId, OrderType = source.OrderType,
            Status = source.Status, Keyword = source.Keyword, IncludeInactive = source.IncludeInactive,
            OnlyNeedAction = source.OnlyNeedAction, PageNumber = source.PageNumber, PageSize = source.PageSize
        };

    internal static IReadOnlyList<PLPUPurchaseReportChartPointDto> Fill(
        IReadOnlyList<PLPUPurchaseReportChartPointDto> source, DateTime from, DateTime to)
    {
        var lookup = source.Where(x => x.PeriodStart.HasValue).ToDictionary(x => x.PeriodStart!.Value.Date);
        var result = new List<PLPUPurchaseReportChartPointDto>();
        for (var month = from; month <= to;)
        {
            result.Add(lookup.TryGetValue(month, out var point) ? point : new()
            {
                Label = month.ToString("yyyy-MM"), PeriodStart = month,
                PeriodEnd = new DateTime(month.Year, month.Month, DateTime.DaysInMonth(month.Year, month.Month))
            });
            if (month.Year == to.Year && month.Month == to.Month) break;
            month = month.AddMonths(1);
        }
        return result;
    }
}
