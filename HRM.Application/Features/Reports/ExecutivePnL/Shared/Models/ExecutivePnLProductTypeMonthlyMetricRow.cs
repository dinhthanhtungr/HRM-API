using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.Reports.ExecutivePnL.Shared.Models
{
    internal sealed class ExecutivePnLProductTypeMonthlyMetricRow : ExecutivePnLMonthlyMetricRow
    {
        public string GroupKey { get; set; } = string.Empty;
        public string GroupLabel { get; set; } = string.Empty;

        public string ProductTypeKey { get; set; } = string.Empty;
        public string ProductTypeLabel { get; set; } = string.Empty;

        public string ProductKey { get; set; } = string.Empty;
        public string ProductLabel { get; set; } = string.Empty;

        public string SaleKey { get; set; } = string.Empty;
        public string SaleLabel { get; set; } = string.Empty;
    }
}
