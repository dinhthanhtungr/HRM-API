using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.Reports.ExecutivePnL.Shared.Models
{
    /// <summary>
    /// Model trung gian cho tab theo tháng
    /// M?i row là m?t tháng
    /// </summary>
    internal sealed class ExecutivePnLTrendMetricRow
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal Revenue { get; set; }
        public decimal CostOfSales { get; set; }
        public decimal Profit => Revenue - CostOfSales;
    }
}

