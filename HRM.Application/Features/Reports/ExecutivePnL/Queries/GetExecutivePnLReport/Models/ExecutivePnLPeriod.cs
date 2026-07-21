using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.Reports.ExecutivePnL.Queries.GetExecutivePnLReport.Models
{
    internal sealed class ExecutivePnLPeriod
    {
        public DateTime FromMonth { get; }
        public DateTime ToMonth { get; }
        public IReadOnlyList<DateTime> Months { get; }

        private ExecutivePnLPeriod(DateTime fromMonth, DateTime toMonth)
        {
            FromMonth = fromMonth;
            ToMonth = toMonth;
            Months = BuildMonths(fromMonth, toMonth);
        }

        public static ExecutivePnLPeriod Create(DateTime? fromMonth, DateTime? toMonth)
        {
            var to = FirstDayOfMonth(toMonth ?? DateTime.Today);
            var from = FirstDayOfMonth(fromMonth ?? to.AddMonths(-5));

            if (from > to)
            {
                (from, to) = (to, from);
            }

            return new ExecutivePnLPeriod(from, to);
        }

        public static string MonthKey(int year, int month)
        {
            return $"actual_{year:D4}_{month:D2}";
        }

        private static List<DateTime> BuildMonths(DateTime fromMonth, DateTime toMonth)
        {
            var months = new List<DateTime>();

            for (var month = fromMonth; month <= toMonth; month = month.AddMonths(1))
            {
                months.Add(month);
            }

            return months;
        }

        private static DateTime FirstDayOfMonth(DateTime value)
        {
            return new DateTime(value.Year, value.Month, 1);
        }
    }
}

