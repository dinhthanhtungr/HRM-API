namespace HRM.Application.Features.Reports.ExecutivePnL.Shared.Models
{
    internal sealed class ExecutivePnLSalesMonthlyMetricRow : ExecutivePnLMonthlyMetricRow
    {
        public string GroupKey { get; set; } = string.Empty;
        public string GroupLabel { get; set; } = string.Empty;
        public string SaleKey { get; set; } = string.Empty;
        public string SaleLabel { get; set; } = string.Empty;
    }
}

