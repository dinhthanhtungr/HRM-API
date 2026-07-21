using HRM.Application.Features.Reports.ExecutivePnL.Shared.Dtos;
using HRM.Application.Features.Reports.ExecutivePnL.Shared.Models;

namespace HRM.Application.Features.Reports.ExecutivePnL.Shared.Services.Builders
{
    internal sealed partial class ExecutivePnLDashboardBuilder
    {
        /// <summary>
        /// Dung tab theo sale co cot dong theo thang/quy/nam, kem chart revenue va profit.
        /// </summary>
        //public static ExecutivePnLDashboardTabDto BuildSalesMonthlyTab(
        //    IReadOnlyList<DateTime> months,
        //    IReadOnlyList<ExecutivePnLSalesMonthlyMetricRow> rows,
        //    int topN,
        //    ExecutivePnLDashboardPeriodType periodType = ExecutivePnLDashboardPeriodType.Month)
        //{
        //    var periods = BuildPeriods(months, periodType);
        //    var visibleRows = BuildVisibleSalesRows(rows, periods);
        //    var saleTotals = BuildTopSaleTotals(rows, topN);

        //    return new ExecutivePnLDashboardTabDto
        //    {
        //        Code = "BY_SALES",
        //        Title = "Theo sale",
        //        Columns = BuildPeriodColumns(periods),
        //        Sections = BuildSalesSections(periods, visibleRows),
        //        Charts = new List<ExecutivePnLChartDto>
        //        {
        //            BuildSalesRevenueProfitChart(saleTotals)
        //        }
        //    };
        //}

        /// <summary>
        /// Dung tab phan tich theo sale, nhom theo sale group va co tong group/tong he thong.
        /// </summary>
        public static ExecutivePnLAnalysisTabDto BuildSalesAnalysisTab(
            IReadOnlyList<DateTime> months,
            IReadOnlyList<ExecutivePnLSalesMonthlyMetricRow> rows,
            int topN,
            ExecutivePnLDashboardPeriodType periodType = ExecutivePnLDashboardPeriodType.Month)
        {
            var periods = BuildPeriods(months, periodType);
            var visibleRows = BuildVisibleSalesRows(rows, periods);
            var saleTotals = BuildTopSaleTotals(rows, topN);

            return new ExecutivePnLAnalysisTabDto
            {
                Code = "BY_SALES",
                Title = "Theo sale",
                Columns = BuildPeriodColumns(periods),
                Metrics = BuildAnalysisMetrics(),
                Sections = BuildSalesAnalysisSections(periods, visibleRows),
                Charts = new List<ExecutivePnLChartDto>
                {
                    BuildSalesRevenueProfitChart(saleTotals),
                    //BuildSalesGroupMetricPieChart(visibleRows),
                    BuildSalesGroupComposedChart(periods, visibleRows),
                    BuildSalesDetailMetricsChart(periods, visibleRows),
                    BuildSalesGroupGrowthChart(periods, visibleRows),
                    BuildSalesDetailGrowthChart(periods, visibleRows),
                }
            };
        }

        /// <summary>
        /// Loc nhung sale co phat sinh doanh thu trong cac period dang hien thi.
        /// </summary>
        private static List<ExecutivePnLSalesMonthlyMetricRow> BuildVisibleSalesRows(
            IReadOnlyList<ExecutivePnLSalesMonthlyMetricRow> rows,
            IReadOnlyList<ExecutivePnLDashboardPeriod> periods)
        {
            var visibleSaleKeys = rows
                .GroupBy(x => x.SaleKey)
                .Where(x => periods.Any(period => x
                    .Where(row => period.Contains(row.Year, row.Month))
                    .Sum(row => row.Revenue) != 0))
                .Select(x => x.Key)
                .ToHashSet();

            return rows
                .Where(x => visibleSaleKeys.Contains(x.SaleKey))
                .ToList();
        }

        /// <summary>
        /// Lay danh sach sale dung cho chart, gioi han theo topN neu topN lon hon 0.
        /// </summary>
        private static List<SalesChartTotal> BuildTopSaleTotals(
            IReadOnlyList<ExecutivePnLSalesMonthlyMetricRow> rows,
            int topN)
        {
            var chartSaleKeys = rows
                .GroupBy(x => x.SaleKey)
                .Select(x => new { SaleKey = x.Key, Revenue = x.Sum(r => r.Revenue) })
                .OrderByDescending(x => x.Revenue)
                .Take(topN)
                .Select(x => x.SaleKey)
                .ToHashSet();

            return rows
                .Where(x => topN <= 0 || chartSaleKeys.Contains(x.SaleKey))
                .GroupBy(x => new { x.GroupKey, x.GroupLabel, x.SaleKey, x.SaleLabel })
                .OrderByDescending(x => x.Sum(r => r.Revenue))
                .Select(x => new SalesChartTotal(
                    x.Key.SaleKey,
                    x.Key.SaleLabel,
                    x.Key.GroupKey,
                    x.Key.GroupLabel,
                    x.Sum(r => r.Revenue),
                    x.Sum(r => r.CostOfSales),
                    x.Sum(r => r.Profit)))
                .ToList();
        }

        /// <summary>
        /// Dùng cho chart so sánh revenue và profit theo sale.
        /// </summary>
        private static ExecutivePnLChartDto BuildSalesRevenueProfitChart(
            IReadOnlyList<SalesChartTotal> saleTotals)
        {
            return new ExecutivePnLChartDto
            {
                Code = "BY_SALES_REVENUE_PROFIT",
                Title = "Theo sale",
                Categories = saleTotals.Select(x => x.SaleLabel).ToList(),
                CategoryItems = saleTotals
                    .Select(x => new ExecutivePnLChartCategoryDto
                    {
                        Key = x.SaleKey,
                        Label = x.SaleLabel,
                        GroupKey = x.GroupKey,
                        GroupLabel = x.GroupLabel
                    })
                    .ToList(),
                Series = new List<ExecutivePnLChartSeriesDto>
                {
                    BuildSeries("revenue", "Doanh thu", "bar", saleTotals.Select(x => x.Revenue)),
                    BuildSeries("profit", "Lợi nhuận", "bar", saleTotals.Select(x => x.Profit))
                }
            };
        }

        private static ExecutivePnLChartDto BuildSalesGroupComposedChart(
            IReadOnlyList<ExecutivePnLDashboardPeriod> periods,
            IReadOnlyList<ExecutivePnLSalesMonthlyMetricRow> rows)
        {
            var selectedPeriod = periods.LastOrDefault();

            var periodRows = selectedPeriod is null
                ? rows
                : rows
                    .Where(x => selectedPeriod.Contains(x.Year, x.Month))
                    .ToList();

            var groups = periodRows
                .GroupBy(x => new { x.GroupKey, x.GroupLabel })
                .OrderByDescending(x => x.Sum(r => r.Revenue))
                .ToList();

            return new ExecutivePnLChartDto
            {
                Code = "BY_SALES_GROUP_COMPOSED",
                Title = "So sánh doanh thu và chỉ số theo group",
                Categories = groups.Select(x => x.Key.GroupLabel).ToList(),
                CategoryItems = groups
                    .Select(x => new ExecutivePnLChartCategoryDto
                    {
                        Key = x.Key.GroupKey,
                        Label = x.Key.GroupLabel
                    })
                    .ToList(),
                Series = new List<ExecutivePnLChartSeriesDto>
                {
                    BuildSeries("revenue", "Doanh thu", "bar", groups
                        .Select(x => Math.Round(x.Sum(r => r.Revenue), 2))),

                    BuildSeries("profit", "Lợi nhuận", "bar", groups
                        .Select(x => Math.Round(x.Sum(r => r.Profit), 2))),

                    BuildSeries("costOfSales", "Giá vốn", "bar", groups
                        .Select(x => Math.Round(x.Sum(r => r.CostOfSales), 2))),

                    BuildSeries("marginPercent", "Biên lợi nhuận", "line", groups
                        .Select(x => CalculateMarginPercent(
                            x.Sum(r => r.Revenue),
                            x.Sum(r => r.Profit))))
                }
            };
        }

        /// <summary>
        /// Dung chart chi tiet theo sale co metadata group, phuc vu bieu do nang cao group + sale.
        /// </summary>
        private static ExecutivePnLChartDto BuildSalesDetailMetricsChart(
            IReadOnlyList<ExecutivePnLDashboardPeriod> periods,
            IReadOnlyList<ExecutivePnLSalesMonthlyMetricRow> rows)
        {
            var selectedPeriod = periods.LastOrDefault();

            var periodRows = selectedPeriod is null
                ? rows
                : rows
            .Where(x => selectedPeriod.Contains(x.Year, x.Month))
            .ToList();

            var sales = periodRows
                .GroupBy(x => new { x.GroupKey, x.GroupLabel, x.SaleKey, x.SaleLabel })
                .OrderBy(x => x.Key.GroupLabel)
                .ThenByDescending(x => x.Sum(r => r.Revenue))
                .Select(x => new SalesChartTotal(
                    x.Key.SaleKey,
                    x.Key.SaleLabel,
                    x.Key.GroupKey,
                    x.Key.GroupLabel,
                    x.Sum(r => r.Revenue),
                    x.Sum(r => r.CostOfSales),
                    x.Sum(r => r.Profit)))
                .ToList();

            return new ExecutivePnLChartDto
            {
                Code = "BY_SALES_DETAIL_METRICS",
                Title = "Chi tiết chỉ số theo sale",
                Categories = sales.Select(x => x.SaleLabel).ToList(),
                CategoryItems = sales
                    .Select(x => new ExecutivePnLChartCategoryDto
                    {
                        Key = x.SaleKey,
                        Label = x.SaleLabel,
                        GroupKey = x.GroupKey,
                        GroupLabel = x.GroupLabel
                    })
                    .ToList(),
                Series = new List<ExecutivePnLChartSeriesDto>
                {
                    BuildSeries("revenue", "Doanh thu", "reference", sales.Select(x => x.Revenue)),
                    BuildSeries("costOfSales", "Gía von", "bar", sales.Select(x => x.CostOfSales)),
                    BuildSeries("profit", "Loi nhuan", "bar", sales.Select(x => x.Profit)),
                    BuildSeries("marginPercent", "Biên lợi nhuận %", "bar", sales.Select(x => x.MarginPercent))
                }
            };
        }

        /// <summary>
        /// Chua tong revenue/cost/profit cua mot sale dung de ve chart.
        /// </summary>
        private sealed record SalesChartTotal(
            string SaleKey,
            string SaleLabel,
            string GroupKey,
            string GroupLabel,
            decimal Revenue,
            decimal CostOfSales,
            decimal Profit)
        {
            public decimal MarginPercent => CalculateMarginPercent(Revenue, Profit);
        }

        /// <summary>
        /// Dung positive/negative bar chart de so sanh tang truong toan cong ty va cac group o ky cuoi so voi ky truoc.
        /// </summary>
        private static ExecutivePnLChartDto BuildSalesGroupGrowthChart(
            IReadOnlyList<ExecutivePnLDashboardPeriod> periods,
            IReadOnlyList<ExecutivePnLSalesMonthlyMetricRow> rows)
        {
            var items = new List<SalesGrowthChartItem>
            {
                new(
                    Key: "TOTAL_COMPANY",
                    Label: "Toàn công ty",
                    GroupKey: null,
                    GroupLabel: null,
                    Rows: rows)
            };

            items.AddRange(rows
                .GroupBy(x => new { x.GroupKey, x.GroupLabel })
                .OrderByDescending(x => x.Sum(r => r.Revenue))
                .Select(x => new SalesGrowthChartItem(
                    Key: x.Key.GroupKey,
                    Label: x.Key.GroupLabel,
                    GroupKey: x.Key.GroupKey,
                    GroupLabel: x.Key.GroupLabel,
                    Rows: x.ToList())));

            return new ExecutivePnLChartDto
            {
                Code = "BY_SALES_GROUP_GROWTH",
                Title = "Tang truong theo group",
                Categories = items.Select(x => x.Label).ToList(),
                CategoryItems = items
                    .Select(x => new ExecutivePnLChartCategoryDto
                    {
                        Key = x.Key,
                        Label = x.Label,
                        GroupKey = x.GroupKey,
                        GroupLabel = x.GroupLabel
                    })
                    .ToList(),
                Series = new List<ExecutivePnLChartSeriesDto>
                {
                    BuildSeries("revenueGrowthPercent", "Tang truong doanh thu %", "bar",
                        items.Select(x => CalculateGrowthPercent(periods, x.Rows, r => r.Revenue))),

                    BuildSeries("profitGrowthPercent", "Tang truong loi nhuan %", "bar",
                        items.Select(x => CalculateGrowthPercent(periods, x.Rows, r => r.Profit)))
                }
            };
        }

        private sealed record SalesGrowthChartItem(
            string Key,
            string Label,
            string? GroupKey,
            string? GroupLabel,
            IReadOnlyList<ExecutivePnLSalesMonthlyMetricRow> Rows);

        /// <summary>
        /// Dung positive/negative bar chart de xem tung sale trong group tang/giam ra sao o ky cuoi so voi ky truoc.
        /// </summary>
        private static ExecutivePnLChartDto BuildSalesDetailGrowthChart(
            IReadOnlyList<ExecutivePnLDashboardPeriod> periods,
            IReadOnlyList<ExecutivePnLSalesMonthlyMetricRow> rows)
        {
            var sales = rows
                .GroupBy(x => new { x.GroupKey, x.GroupLabel, x.SaleKey, x.SaleLabel })
                .OrderBy(x => x.Key.GroupLabel)
                .ThenByDescending(x => x.Sum(r => r.Revenue))
                .ToList();

            return new ExecutivePnLChartDto
            {
                Code = "BY_SALES_DETAIL_GROWTH",
                Title = "Tang truong theo sale",
                Categories = sales.Select(x => x.Key.SaleLabel).ToList(),
                CategoryItems = sales
                    .Select(x => new ExecutivePnLChartCategoryDto
                    {
                        Key = x.Key.SaleKey,
                        Label = x.Key.SaleLabel,
                        GroupKey = x.Key.GroupKey,
                        GroupLabel = x.Key.GroupLabel
                    })
                    .ToList(),
                Series = new List<ExecutivePnLChartSeriesDto>
                {
                    BuildSeries("revenueGrowthPercent", "Tang truong doanh thu %", "bar",
                        sales.Select(x => CalculateGrowthPercent(periods, x.ToList(), r => r.Revenue))),
                    BuildSeries("profitGrowthPercent", "Tang truong loi nhuan %", "bar",
                        sales.Select(x => CalculateGrowthPercent(periods, x.ToList(), r => r.Profit)))
                }
            };
        }

        /// <summary>
        /// Dung cac section legacy cho tab sale chi hien thi mot metric doanh thu.
        /// </summary>
        private static List<ExecutivePnLSectionDto> BuildSalesSections(
            IReadOnlyList<ExecutivePnLDashboardPeriod> periods,
            IReadOnlyList<ExecutivePnLSalesMonthlyMetricRow> rows)
        {
            var sortOrder = 10;
            var sections = rows
                .GroupBy(x => new { x.GroupKey, x.GroupLabel })
                .OrderBy(x => x.Key.GroupLabel)
                .Select(group =>
                {
                    var section = new ExecutivePnLSectionDto
                    {
                        Code = group.Key.GroupKey,
                        Title = group.Key.GroupLabel,
                        SortOrder = sortOrder,
                        Lines = BuildSalesLines(periods, group.ToList())
                    };

                    sortOrder += 10;
                    return section;
                })
                .ToList();

            if (rows.Count > 0)
            {
                sections.Add(new ExecutivePnLSectionDto
                {
                    Code = "TOTAL_SYSTEM",
                    Title = "Tong",
                    SortOrder = sortOrder,
                    Lines = new List<ExecutivePnLLineDto>
                    {
                        BuildSalesSummaryLine("TOTAL_SYSTEM", "Tong", 10, periods, rows)
                    }
                });
            }

            return sections;
        }

        /// <summary>
        /// Dung cac section analysis cho tab sale voi day du metric.
        /// </summary>
        private static List<ExecutivePnLAnalysisSectionDto> BuildSalesAnalysisSections(
            IReadOnlyList<ExecutivePnLDashboardPeriod> periods,
            IReadOnlyList<ExecutivePnLSalesMonthlyMetricRow> rows)
        {
            var sections = new List<ExecutivePnLAnalysisSectionDto>();

            if (rows.Count > 0)
            {
                sections.Add(new ExecutivePnLAnalysisSectionDto
                {
                    Code = "TOTAL_SYSTEM",
                    Title = "Tổng",
                    SortOrder = 10,
                    Lines = new List<ExecutivePnLAnalysisLineDto>
                    {
                        BuildSalesAnalysisSummaryLine("TOTAL_SYSTEM", "Tổng", 10, periods, rows)
                    }
                });
            }

            var sortOrder = 20;

            sections.AddRange(rows
                .GroupBy(x => new { x.GroupKey, x.GroupLabel })
                .OrderBy(x => x.Key.GroupLabel)
                .Select(group =>
                {
                    var section = new ExecutivePnLAnalysisSectionDto
                    {
                        Code = group.Key.GroupKey,
                        Title = group.Key.GroupLabel,
                        SortOrder = sortOrder,
                        Lines = BuildSalesAnalysisLines(periods, group.ToList())
                    };

                    sortOrder += 10;
                    return section;
                }));

            return sections;
        }

        /// <summary>
        /// Dung cac dong sale trong mot group va them dong tong theo group.
        /// </summary>
        private static List<ExecutivePnLAnalysisLineDto> BuildSalesAnalysisLines(
            IReadOnlyList<ExecutivePnLDashboardPeriod> periods,
            IReadOnlyList<ExecutivePnLSalesMonthlyMetricRow> rows)
        {
            var sortOrder = 10;

            var lines = new List<ExecutivePnLAnalysisLineDto>
            {
                BuildSalesAnalysisSummaryLine("GROUP_TOTAL", "Tổng theo nhóm", sortOrder, periods, rows)
            };

            sortOrder += 10;

            lines.AddRange(rows
                .GroupBy(x => new { x.SaleKey, x.SaleLabel })
                .OrderByDescending(x => x.Sum(r => r.Revenue))
                .Select(sale =>
                {
                    var line = new ExecutivePnLAnalysisLineDto
                    {
                        Code = sale.Key.SaleKey,
                        Label = sale.Key.SaleLabel,
                        SortOrder = sortOrder,
                        MetricValues = BuildSalesMetricValues(periods, sale.ToList())
                    };

                    sortOrder += 10;
                    return line;
                }));

            return lines;
        }

        /// <summary>
        /// Dung dong tong trong tab analysis sale.
        /// </summary>
        private static ExecutivePnLAnalysisLineDto BuildSalesAnalysisSummaryLine(
            string code,
            string label,
            int sortOrder,
            IReadOnlyList<ExecutivePnLDashboardPeriod> periods,
            IReadOnlyList<ExecutivePnLSalesMonthlyMetricRow> rows)
        {
            return new ExecutivePnLAnalysisLineDto
            {
                Code = code,
                Label = label,
                SortOrder = sortOrder,
                IsBold = true,
                IsSubtotal = true,
                MetricValues = BuildSalesMetricValues(periods, rows)
            };
        }

        /// <summary>
        /// Dung bo gia tri revenue, cost, profit va margin theo tung period cho tab sale.
        /// </summary>
        private static Dictionary<string, Dictionary<string, decimal>> BuildSalesMetricValues(
            IReadOnlyList<ExecutivePnLDashboardPeriod> periods,
            IReadOnlyList<ExecutivePnLSalesMonthlyMetricRow> rows)
        {
            return new Dictionary<string, Dictionary<string, decimal>>
            {
                ["revenue"] = BuildPeriodValues(periods, rows, x => x.Revenue),
                ["costOfSales"] = BuildPeriodValues(periods, rows, x => x.CostOfSales),
                ["profit"] = BuildPeriodValues(periods, rows, x => x.Profit),
                ["marginPercent"] = BuildMarginPercentPeriodValues(periods, rows)
            };
        }



        /// <summary>
        /// Tinh bien loi nhuan phan tram tu revenue va profit.
        /// </summary>
        private static decimal CalculateMarginPercent(decimal revenue, decimal profit)
        {
            return revenue == 0 ? 0 : Math.Round(profit / revenue * 100, 2);
        }

        /// <summary>
        /// Dung cac dong sale legacy chi co value doanh thu.
        /// </summary>
        private static List<ExecutivePnLLineDto> BuildSalesLines(
            IReadOnlyList<ExecutivePnLDashboardPeriod> periods,
            IReadOnlyList<ExecutivePnLSalesMonthlyMetricRow> rows)
        {
            var sortOrder = 10;
            var lines = rows
                .GroupBy(x => new { x.SaleKey, x.SaleLabel })
                .OrderByDescending(x => x.Sum(r => r.Revenue))
                .Select(sale =>
                {
                    var line = new ExecutivePnLLineDto
                    {
                        Code = sale.Key.SaleKey,
                        Label = sale.Key.SaleLabel,
                        SortOrder = sortOrder,
                        Values = BuildPeriodValues(periods, sale.ToList(), x => x.Revenue)
                    };

                    sortOrder += 10;
                    return line;
                })
                .ToList();

            lines.Add(BuildSalesSummaryLine("GROUP_TOTAL", "Tổng theo nhom", sortOrder, periods, rows));

            return lines;
        }

        /// <summary>
        /// Dung dong tong legacy cho sale/group/system.
        /// </summary>
        private static ExecutivePnLLineDto BuildSalesSummaryLine(
            string code,
            string label,
            int sortOrder,
            IReadOnlyList<ExecutivePnLDashboardPeriod> periods,
            IReadOnlyList<ExecutivePnLSalesMonthlyMetricRow> rows)
        {
            return new ExecutivePnLLineDto
            {
                Code = code,
                Label = label,
                SortOrder = sortOrder,
                IsBold = true,
                IsSubtotal = true,
                Values = BuildPeriodValues(periods, rows, x => x.Revenue)
            };
        }
    }
}
