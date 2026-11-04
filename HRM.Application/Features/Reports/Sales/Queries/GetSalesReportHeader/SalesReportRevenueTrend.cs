using HRM.Application.Commons.Reporting;

namespace HRM.Application.Features.Reports.Sales.Queries.GetSalesReportHeader;

internal static class SalesReportRevenueTrend
{
    public static DateTime Start(DateTime to)
    {
        var month = new DateTime(to.Year, to.Month, 1);
        return month.AddMonths(-Math.Min(5, (to.Year - 1) * 12 + to.Month - 1));
    }

    public static IReadOnlyList<SalesReportMonthlyRevenueDto> Build(DateTime to,
        IReadOnlyList<DeliveryRevenueLine> lines)
    {
        var result = new List<SalesReportMonthlyRevenueDto>();
        for (var month = Start(to); month <= to; month = month.AddMonths(1))
        {
            var end = month.Year == to.Year && month.Month == to.Month ? to.Date : month.AddMonths(1).AddDays(-1);
            result.Add(new(month, month, end, lines
                .Where(x => x.RevenueDate >= month && x.RevenueDate < end.AddDays(1))
                .Sum(x => x.RevenueAmountVnd)));
            if (month.Year == to.Year && month.Month == to.Month)
                break;
        }
        return result;
    }
}
