using HRM.Application.Features.Reports.ExecutivePnL.Queries.GetExecutivePnLReport.Models;
using HRM.Application.Features.Reports.ExecutivePnL.Shared.Dtos;
using HRM.Application.Features.Reports.ExecutivePnL.Shared.Models;

namespace HRM.Application.Features.Reports.ExecutivePnL.Shared.Services.Builders
{
    internal sealed partial class ExecutivePnLDashboardBuilder
    {
        /// <summary>
        /// Dung dictionary gia tri theo period, kem cot total va growth percent.
        /// </summary>
        private static Dictionary<string, decimal> BuildPeriodValues<T>(
            IReadOnlyList<ExecutivePnLDashboardPeriod> periods,
            IReadOnlyList<T> rows,
            Func<T, decimal> selector)
            where T : ExecutivePnLMonthlyMetricRow
        {
            var values = new Dictionary<string, decimal>();

            foreach (var period in periods)
            {
                values[period.Key] = Math.Round(rows
                    .Where(x => period.Contains(x.Year, x.Month))
                    .Sum(selector), 2);
            }

            values[TotalKey] = Math.Round(periods.Sum(period => rows
                .Where(x => period.Contains(x.Year, x.Month))
                .Sum(selector)), 2);

            return values;
        }


        /// <summary>
        /// Tinh margin percent theo tung period va cot tong/growth.
        /// </summary>
        private static Dictionary<string, decimal> BuildMarginPercentPeriodValues<T>(
            IReadOnlyList<ExecutivePnLDashboardPeriod> periods,
            IReadOnlyList<T> rows)
            where T : ExecutivePnLMonthlyMetricRow
        {
            var values = new Dictionary<string, decimal>();

            foreach (var period in periods)
            {
                var periodRows = rows
                    .Where(x => period.Contains(x.Year, x.Month))
                    .ToList();

                values[period.Key] = CalculateMarginPercent(
                    periodRows.Sum(x => x.Revenue),
                    periodRows.Sum(x => x.Profit));
            }

            var visibleRows = periods
                .SelectMany(period => rows.Where(x => period.Contains(x.Year, x.Month)))
                .ToList();

            values[TotalKey] = CalculateMarginPercent(
                visibleRows.Sum(x => x.Revenue),
                visibleRows.Sum(x => x.Profit));

            return values;
        }

        /// <summary>
        /// Dung danh sach cot theo period va them cot total/growth o cuoi bang.
        /// </summary>
        private static List<ExecutivePnLColumnDto> BuildPeriodColumns(
            IReadOnlyList<ExecutivePnLDashboardPeriod> periods)
        {
            var columns = periods
                .Select(x => new ExecutivePnLColumnDto
                {
                    Key = x.Key,
                    Label = x.Label,
                    ColumnType = "Actual"
                })
                .ToList();

            columns.Add(new ExecutivePnLColumnDto
            {
                Key = TotalKey,
                Label = "Total actual",
                ColumnType = "ActualTotal"
            });

            return columns;
        }

        /// <summary>
        /// Tinh ty le tang truong cua period cuoi cung so voi period lien truoc cung do dai.
        /// </summary>
        private static decimal CalculateGrowthPercent(
            IReadOnlyList<ExecutivePnLDashboardPeriod> periods,
            IReadOnlyList<ExecutivePnLSalesMonthlyMetricRow> rows,
            Func<ExecutivePnLSalesMonthlyMetricRow, decimal> selector)
        {
            var currentPeriod = periods.LastOrDefault();

            if (currentPeriod is null)
            {
                return 0;
            }

            var current = rows
                .Where(x => currentPeriod.Contains(x.Year, x.Month))
                .Sum(selector);
            var previous = rows
                .Where(x => currentPeriod.PreviousContains(x.Year, x.Month))
                .Sum(selector);

            if (previous == 0)
            {
                return current == 0 ? 0 : 100;
            }

            return Math.Round((current - previous) / previous * 100, 2);
        }

        /// <summary>
        /// Chuyen danh sach thang thanh danh sach period theo kieu thang, quy hoac nam.
        /// </summary>
        private static List<ExecutivePnLDashboardPeriod> BuildPeriods(
            IReadOnlyList<DateTime> months,
            ExecutivePnLDashboardPeriodType periodType)
        {
            return periodType switch
            {
                ExecutivePnLDashboardPeriodType.Year => months
                    .GroupBy(x => x.Year)
                    .Select(x => ExecutivePnLDashboardPeriod.Year(x.Key))
                    .ToList(),
                ExecutivePnLDashboardPeriodType.Quarter => months
                    .GroupBy(x => new { x.Year, Quarter = ((x.Month - 1) / 3) + 1 })
                    .Select(x => ExecutivePnLDashboardPeriod.Quarter(x.Key.Year, x.Key.Quarter))
                    .ToList(),
                _ => months
                    .Select(x => ExecutivePnLDashboardPeriod.Month(x.Year, x.Month))
                    .ToList()
            };
        }

        /// <summary>
        /// Mo ta mot cot period tren dashboard, co the dai mot thang, mot quy hoac mot nam.
        /// </summary>
        private sealed class ExecutivePnLDashboardPeriod
        {
            private ExecutivePnLDashboardPeriod(
                string key,
                string label,
                int year,
                int startMonth,
                int endMonth,
                ExecutivePnLDashboardPeriodType periodType)
            {
                Key = key;
                Label = label;
                PeriodYear = year;
                StartMonth = startMonth;
                EndMonth = endMonth;
                PeriodType = periodType;
            }

            public string Key { get; }
            public string Label { get; }
            private int PeriodYear { get; }
            private int StartMonth { get; }
            private int EndMonth { get; }
            private ExecutivePnLDashboardPeriodType PeriodType { get; }

            /// <summary>
            /// Tao period dai mot thang.
            /// </summary>
            public static ExecutivePnLDashboardPeriod Month(int year, int month)
            {
                return new ExecutivePnLDashboardPeriod(
                    ExecutivePnLPeriod.MonthKey(year, month),
                    $"{year}-{month:00}",
                    year,
                    month,
                    month,
                    ExecutivePnLDashboardPeriodType.Month);
            }

            /// <summary>
            /// Tao period dai mot quy.
            /// </summary>
            public static ExecutivePnLDashboardPeriod Quarter(int year, int quarter)
            {
                var startMonth = ((quarter - 1) * 3) + 1;

                return new ExecutivePnLDashboardPeriod(
                    $"actual_{year:D4}_q{quarter}",
                    $"{year}-Q{quarter}",
                    year,
                    startMonth,
                    startMonth + 2,
                    ExecutivePnLDashboardPeriodType.Quarter);
            }

            /// <summary>
            /// Tao period dai mot nam.
            /// </summary>
            public static ExecutivePnLDashboardPeriod Year(int year)
            {
                return new ExecutivePnLDashboardPeriod(
                    $"actual_{year:D4}",
                    year.ToString(),
                    year,
                    1,
                    12,
                    ExecutivePnLDashboardPeriodType.Year);
            }

            /// <summary>
            /// Kiem tra mot thang co nam trong period hien tai khong.
            /// </summary>
            public bool Contains(int year, int month)
            {
                return year == PeriodYear && month >= StartMonth && month <= EndMonth;
            }

            /// <summary>
            /// Kiem tra mot thang co nam trong period lien truoc khong.
            /// </summary>
            public bool PreviousContains(int year, int month)
            {
                var previousStart = new DateTime(PeriodYear, StartMonth, 1).AddMonths(-PeriodLengthInMonths());
                var previousEnd = new DateTime(PeriodYear, EndMonth, 1).AddMonths(-PeriodLengthInMonths());

                return year == previousStart.Year
                    && month >= previousStart.Month
                    && year == previousEnd.Year
                    && month <= previousEnd.Month;
            }

            /// <summary>
            /// Lay do dai period tinh theo thang.
            /// </summary>
            private int PeriodLengthInMonths()
            {
                return PeriodType switch
                {
                    ExecutivePnLDashboardPeriodType.Year => 12,
                    ExecutivePnLDashboardPeriodType.Quarter => 3,
                    _ => 1
                };
            }
        }
    }
}
