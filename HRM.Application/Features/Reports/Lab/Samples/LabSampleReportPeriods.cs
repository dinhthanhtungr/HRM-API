namespace HRM.Application.Features.Reports.Lab.Samples;

internal static class LabSampleReportPeriods
{
    public const string Timezone = "Asia/Ho_Chi_Minh";

    public static DateOnly TrendStart(DateOnly to) => new DateOnly(to.Year, to.Month, 1).AddMonths(-5);

    // Các cột nguồn là timestamp without time zone: ngày giờ địa phương nghiệp vụ UTC+7,
    // không chuyển local midnight thành UTC rồi so sánh với cột local.
    public static DateTime LocalMidnight(DateOnly date) => date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);

    public static IReadOnlyList<LabSampleMonthDto> BuildMonths(DateOnly to, IReadOnlyList<LabSampleAggregate> aggregates)
    {
        var first = TrendStart(to);
        var result = new List<LabSampleMonthDto>(6);
        for (var i = 0; i < 6; i++)
        {
            var start = first.AddMonths(i);
            var end = new DateOnly(start.Year, start.Month, DateTime.DaysInMonth(start.Year, start.Month));
            var periodTo = end < to ? end : to;
            var metrics = LabSampleAggregation.ToDto(aggregates.FirstOrDefault(x => x.Key == start.Year * 100 + start.Month));
            result.Add(new(start.Year, start.Month, metrics)
            {
                PeriodFrom = start, PeriodTo = periodTo, IsPartialMonth = periodTo < end,
                DataStatus = metrics.RequestCount == 0 ? "no_requests" : "available",
                DataUnavailableReason = null
            });
        }
        return result;
    }
}
