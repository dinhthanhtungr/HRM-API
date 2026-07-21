using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.PLM.Dashboard.Shared.Services
{
    internal static class PlmDashboardMath
    {
        public static int GetInclusiveMonthCount(DateTime fromMonth, DateTime toMonth)
        {
            if (fromMonth > toMonth) return 0;

            return ((toMonth.Year - fromMonth.Year) * 12)
                + toMonth.Month
                - fromMonth.Month
                + 1;
        }

        public static DateTime Max(DateTime left, DateTime right)
            => left >= right ? left : right;

        public static DateTime Min(DateTime left, DateTime right)
            => left <= right ? left : right;

        public static decimal CalculateRate(int value, int total)
        {
            if (total == 0) return 0;

            return Math.Round(value * 100m / total, 2);
        }
    }
}
