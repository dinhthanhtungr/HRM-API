using HRM.Application.Features.Reports.ExecutivePnL.Shared.Dtos;
using HRM.Application.Features.Reports.ExecutivePnL.Shared.Models;

namespace HRM.Application.Features.Reports.ExecutivePnL.Shared.Services.Builders
{
    internal sealed partial class ExecutivePnLDashboardBuilder
    {

        /// <summary>
        /// Dung tab phan tich theo khach hang voi nhieu metric de FE co the doi metric hien thi.
        /// </summary>
        public static ExecutivePnLAnalysisTabDto BuildCustomerAnalysisTab(
            IReadOnlyList<ExecutivePnLDashboardMetricRow> rows)
        {
            return BuildMetricAnalysisTab("BY_CUSTOMER", "Theo khach hang", rows);
        }

        /// <summary>
        /// Dung tab phan tich dang dimension, moi dong co day du revenue, cost, profit va margin.
        /// </summary>
        private static ExecutivePnLAnalysisTabDto BuildMetricAnalysisTab(
            string code,
            string title,
            IReadOnlyList<ExecutivePnLDashboardMetricRow> rows)
        {
            var orderedRows = rows.OrderByDescending(x => x.Revenue).ToList();
            var categories = orderedRows.Select(x => x.Label).ToList();

            return new ExecutivePnLAnalysisTabDto
            {
                Code = code,
                Title = title,
                Columns = new List<ExecutivePnLColumnDto>
                {
                    new()
                    {
                        Key = TotalKey,
                        Label = "Total actual",
                        ColumnType = "ActualTotal"
                    }
                },
                Metrics = BuildAnalysisMetrics(),
                Sections = new List<ExecutivePnLAnalysisSectionDto>
                {
                    new()
                    {
                        Code = "DETAIL",
                        Title = title,
                        SortOrder = 10,
                        Lines = orderedRows
                            .Select((row, index) => new ExecutivePnLAnalysisLineDto
                            {
                                Code = row.Key,
                                Label = row.Label,
                                SortOrder = (index + 1) * 10,
                                MetricValues = BuildMetricRowValues(row)
                            })
                            .Append(new ExecutivePnLAnalysisLineDto
                            {
                                Code = "TOTAL_SYSTEM",
                                Label = "Tổng",
                                SortOrder = (orderedRows.Count + 1) * 10,
                                IsBold = true,
                                IsSubtotal = true,
                                MetricValues = BuildMetricRowValues(new ExecutivePnLDashboardMetricRow
                                {
                                    Key = "TOTAL_SYSTEM",
                                    Label = "Tổng",
                                    Revenue = orderedRows.Sum(x => x.Revenue),
                                    CostOfSales = orderedRows.Sum(x => x.CostOfSales)
                                })
                            })
                            .ToList()
                    }
                },
                Charts = new List<ExecutivePnLChartDto>
                {
                    new()
                    {
                        Code = $"{code}_REVENUE_PROFIT",
                        Title = title,
                        Categories = categories,
                        CategoryItems = orderedRows
                            .Select(x => new ExecutivePnLChartCategoryDto
                            {
                                Key = x.Key,
                                Label = x.Label
                            })
                            .ToList(),
                        Series = new List<ExecutivePnLChartSeriesDto>
                        {
                            BuildSeries("revenue", "Revenue", "bar", orderedRows.Select(x => x.Revenue)),
                            BuildSeries("profit", "Profit", "bar", orderedRows.Select(x => x.Profit))
                        }
                    }
                }
            };
        }

        /// <summary>
        /// Dung bo gia tri metric cua mot dong dimension.
        /// </summary>
        private static Dictionary<string, Dictionary<string, decimal>> BuildMetricRowValues(
            ExecutivePnLDashboardMetricRow row)
        {
            return new Dictionary<string, Dictionary<string, decimal>>
            {
                ["revenue"] = new() { [TotalKey] = Math.Round(row.Revenue, 2) },
                ["costOfSales"] = new() { [TotalKey] = Math.Round(row.CostOfSales, 2) },
                ["profit"] = new() { [TotalKey] = Math.Round(row.Profit, 2) },
                ["marginPercent"] = new() { [TotalKey] = Math.Round(row.MarginPercent, 2) }
            };
        }

        /// <summary>
        /// Khai bao danh sach metric FE co the chon de hien thi trong cac tab analysis.
        /// </summary>
        private static List<ExecutivePnLAnalysisMetricDto> BuildAnalysisMetrics()
        {
            return new List<ExecutivePnLAnalysisMetricDto>
            {
                new() { Key = "revenue", Label = "Doanh thu", ValueType = "Money" },
                new() { Key = "costOfSales", Label = "Giá vốn", ValueType = "Money" },
                new() { Key = "profit", Label = "Lợi Nhuận", ValueType = "Money" },
                new() { Key = "marginPercent", Label = "Biên lợi nhuận %", ValueType = "Percent" }
            };
        }
    }
}
