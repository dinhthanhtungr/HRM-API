using HRM.Application.Features.Reports.ExecutivePnL.Shared.Dtos;
using HRM.Application.Features.Reports.ExecutivePnL.Shared.Models;

namespace HRM.Application.Features.Reports.ExecutivePnL.Shared.Services.Builders
{
    internal sealed partial class ExecutivePnLDashboardBuilder
    {
        /// <summary>
        /// Dung tab doanh thu/loi nhuan tong hop theo sale tu du lieu metric dang dimension.
        /// </summary>
        public static ExecutivePnLDashboardTabDto BuildSalesTab(
            IReadOnlyList<ExecutivePnLDashboardMetricRow> rows)
        {
            return BuildMetricTab("BY_SALES", "Theo sale", rows);
        }

        /// <summary>
        /// Dung tab doanh thu/loi nhuan theo loai san pham.
        /// </summary>
        public static ExecutivePnLDashboardTabDto BuildProductTypeTab(
            IReadOnlyList<ExecutivePnLDashboardMetricRow> rows)
        {
            return BuildMetricTab("BY_PRODUCT_TYPE", "Theo loai san pham", rows);
        }

        /// <summary>
        /// Dung tab doanh thu/loi nhuan theo khach hang.
        /// </summary>
        public static ExecutivePnLDashboardTabDto BuildCustomerTab(
            IReadOnlyList<ExecutivePnLDashboardMetricRow> rows)
        {
            return BuildMetricTab("BY_CUSTOMER", "Theo khach hang", rows);
        }

        /// <summary>
        /// Dung tab xu huong theo thoi gian cho doanh thu va loi nhuan.
        /// </summary>
        public static ExecutivePnLDashboardTabDto BuildTrendTab(
            IReadOnlyList<ExecutivePnLTrendMetricRow> rows)
        {
            var categories = rows.Select(x => $"{x.Year}-{x.Month:00}").ToList();

            return new ExecutivePnLDashboardTabDto
            {
                Code = "TREND",
                Title = "Tong quan theo thang",
                Charts = new List<ExecutivePnLChartDto>
                {
                    new()
                    {
                        Code = "TREND_REVENUE_PROFIT",
                        Title = "Revenue vs Profit",
                        Categories = categories,
                        CategoryItems = BuildCategoryItems(categories),
                        Series = new List<ExecutivePnLChartSeriesDto>
                        {
                            BuildSeries("revenue", "Revenue", "bar", rows.Select(x => x.Revenue)),
                            BuildSeries("profit", "Profit", "line", rows.Select(x => x.Profit))
                        }
                    }
                }
            };
        }

        /// <summary>
        /// Dung tab metric co mot section financial va mot chart revenue/profit.
        /// </summary>
        private static ExecutivePnLDashboardTabDto BuildMetricTab(
            string code,
            string title,
            IReadOnlyList<ExecutivePnLDashboardMetricRow> rows)
        {
            var columns = rows.Select(x => new ExecutivePnLColumnDto
            {
                Key = x.Key,
                Label = x.Label,
                ColumnType = "Dimension"
            }).ToList();

            columns.Add(new ExecutivePnLColumnDto
            {
                Key = TotalKey,
                Label = "Total",
                ColumnType = "ActualTotal"
            });

            var categories = rows.Select(x => x.Label).ToList();

            return new ExecutivePnLDashboardTabDto
            {
                Code = code,
                Title = title,
                Columns = columns,
                Sections = new List<ExecutivePnLSectionDto>
                {
                    new()
                    {
                        Code = "FINANCIAL",
                        Title = "Financial",
                        SortOrder = 10,
                        Lines = new List<ExecutivePnLLineDto>
                        {
                            BuildMetricLine("REVENUE", "Revenue", 10, rows, x => x.Revenue),
                            BuildMetricLine("COST_OF_SALES", "Cost of sales", 20, rows, x => x.CostOfSales),
                            BuildMetricLine("PROFIT", "Profit", 30, rows, x => x.Profit),
                            BuildMetricLine("MARGIN_PERCENT", "Margin %", 40, rows, x => x.MarginPercent)
                        }
                    }
                },
                Charts = new List<ExecutivePnLChartDto>
                {
                    new()
                    {
                        Code = $"{code}_REVENUE_PROFIT",
                        Title = title,
                        Categories = categories,
                        CategoryItems = rows
                            .Select(x => new ExecutivePnLChartCategoryDto
                            {
                                Key = x.Key,
                                Label = x.Label
                            })
                            .ToList(),
                        Series = new List<ExecutivePnLChartSeriesDto>
                        {
                            BuildSeries("revenue", "Revenue", "bar", rows.Select(x => x.Revenue)),
                            BuildSeries("profit", "Profit", "bar", rows.Select(x => x.Profit))
                        }
                    }
                }
            };
        }

        /// <summary>
        /// Dung mot dong metric financial va tinh tong cua dong do.
        /// </summary>
        private static ExecutivePnLLineDto BuildMetricLine(
            string code,
            string label,
            int sortOrder,
            IReadOnlyList<ExecutivePnLDashboardMetricRow> rows,
            Func<ExecutivePnLDashboardMetricRow, decimal> selector)
        {
            var values = rows.ToDictionary(x => x.Key, x => Math.Round(selector(x), 2));
            values[TotalKey] = Math.Round(rows.Sum(selector), 2);

            return new ExecutivePnLLineDto
            {
                Code = code,
                Label = label,
                SortOrder = sortOrder,
                Values = values
            };
        }
    }
}
