using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.Reports.ExecutivePnL.Shared.Models
{
    /// <summary>
    /// Model chung gian cho tab sale/product/customer
    /// Mỗi Row là một sale hoặc một sản phẩm hay khách hàng
    /// </summary>
    internal sealed class ExecutivePnLDashboardMetricRow
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
        public decimal CostOfSales { get; set; }
        public decimal Profit => Revenue - CostOfSales;
        public decimal MarginPercent => Revenue == 0 ? 0 : Math.Round(Profit / Revenue * 100, 2);
    }
}

