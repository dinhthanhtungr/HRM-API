using HRM.Application.Features.Reports.ExecutivePnL.Queries.GetExecutivePnLReport.Services;
using HRM.Application.Features.Reports.ExecutivePnL.Shared.Dtos;
using HRM.Application.Features.Reports.ExecutivePnL.Shared.Models;

namespace HRM.Application.Features.Reports.ExecutivePnL.Shared.Services.Builders
{
    /// <summary>
    /// Dung de dung DTO dashboard Executive PnL tu cac dong metric da duoc reader tong hop.
    /// </summary>
    internal sealed partial class ExecutivePnLDashboardBuilder
    {
        private const string TotalKey = "total_actual";

        /// <summary>
        /// Dung dashboard tong gom cac tab trend, sale, product type va customer.
        /// </summary>
        public ExecutivePnLDashboardDto Build(
            ExecutivePnLFilter filter,
            IReadOnlyList<ExecutivePnLDashboardMetricRow> salesRows,
            IReadOnlyList<ExecutivePnLDashboardMetricRow> productTypeRows,
            IReadOnlyList<ExecutivePnLDashboardMetricRow> customerRows,
            IReadOnlyList<ExecutivePnLTrendMetricRow> trendRows)
        {
            return new ExecutivePnLDashboardDto
            {
                CompanyName = filter.CompanyId?.ToString() ?? "VIETAUS POLYMER",
                BusinessUnit = string.IsNullOrWhiteSpace(filter.BusinessUnit) ? "ALL BUS" : filter.BusinessUnit,
                Currency = string.IsNullOrWhiteSpace(filter.Currency) ? "VND" : filter.Currency,
                FromMonth = filter.FromMonth,
                ToMonth = filter.ToMonth,
                Tabs = new List<ExecutivePnLDashboardTabDto>
                {
                    BuildTrendTab(trendRows),
                    BuildSalesTab(salesRows),
                    BuildProductTypeTab(productTypeRows),
                    BuildCustomerTab(customerRows)
                }
            };
        }
    }
}
