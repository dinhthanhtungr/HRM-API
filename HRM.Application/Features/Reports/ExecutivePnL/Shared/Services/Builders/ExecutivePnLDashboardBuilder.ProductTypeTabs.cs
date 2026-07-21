using HRM.Application.Features.Reports.ExecutivePnL.Shared.Dtos;
using HRM.Application.Features.Reports.ExecutivePnL.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.Reports.ExecutivePnL.Shared.Services.Builders
{
    internal sealed partial class ExecutivePnLDashboardBuilder
    {
        /// <summary>
        /// Dung tab phan tich theo loai san pham voi nhieu metric de FE co the doi metric hien thi.
        /// </summary>
        public static ExecutivePnLAnalysisTabDto BuildProductTypeAnalysisTab(
            IReadOnlyList<DateTime> months,
            IReadOnlyList<ExecutivePnLProductTypeMonthlyMetricRow> rows,
            int topN = 100,
            ExecutivePnLDashboardPeriodType periodType = ExecutivePnLDashboardPeriodType.Month)
        {
            var periods = BuildPeriods(months, periodType);
            var visibleRows = BuildVisibleProductTypeRows(rows, periods);
            var productTotals = BuildTopProductTypeTotals(rows, topN);

            return new ExecutivePnLAnalysisTabDto
            {
                Code = "BY_PRODUCTS",
                Title = "Theo sản phẩm",
                Columns = BuildPeriodColumns(periods),
                Metrics = BuildAnalysisMetrics(),
                Sections = BuildProductTypeAnalysisSections(periods, visibleRows, topN),
                Charts = new List<ExecutivePnLChartDto>
                {
                    BuildProductTypeComposedChart(periods, visibleRows)
                }
            };
        }

        /// <summary>
        /// Lọc những sản phẩm có phát sinh doanh thu trong cac period đang hiển thị.
        /// </summary>
        private static List<ExecutivePnLProductTypeMonthlyMetricRow> BuildVisibleProductTypeRows(
            IReadOnlyList<ExecutivePnLProductTypeMonthlyMetricRow> rows,
            IReadOnlyList<ExecutivePnLDashboardPeriod> periods)
        {
            var visibleSaleKeys = rows
                .GroupBy(x => x.ProductKey)
                .Where(x => periods.Any(period => x
                    .Where(row => period.Contains(row.Year, row.Month))
                    .Sum(row => row.Revenue) != 0))
                .Select(x => x.Key)
                .ToHashSet();

            return rows
                .Where(x => visibleSaleKeys.Contains(x.ProductKey))
                .ToList();
        }


        /// <summary>
        /// Lay danh sach sale dung cho chart, gioi han theo topN neu topN lon hon 0.
        /// </summary>
        private static List<ProductChartTotal> BuildTopProductTypeTotals(
            IReadOnlyList<ExecutivePnLProductTypeMonthlyMetricRow> rows,
            int topN)
        {
            var chartSaleKeys = rows
                .GroupBy(x => x.ProductTypeKey)
                .Select(x => new { ProductTypeKey = x.Key, Revenue = x.Sum(r => r.Revenue) })
                .OrderByDescending(x => x.Revenue)
                .Take(topN)
                .Select(x => x.ProductTypeKey)
                .ToHashSet();

            return rows
                .Where(x => topN <= 0 || chartSaleKeys.Contains(x.ProductTypeKey))
                .GroupBy(x => new { x.ProductTypeKey, x.ProductTypeLabel })
                .OrderByDescending(x => x.Sum(r => r.Revenue))
                .Select(x => new ProductChartTotal(
                    x.Key.ProductTypeKey,
                    x.Key.ProductTypeLabel,
                    x.Sum(r => r.OrderQuantity),
                    x.Sum(r => r.Revenue),
                    x.Sum(r => r.CostOfSales),
                    x.Sum(r => r.Profit)))
                .ToList();
        }

        /// <summary>
        /// Chua tong revenue/cost/profit cua mot sale dung de ve chart.
        /// </summary>
        private sealed record ProductChartTotal(
            string ProductTypeKey,
            string ProductTypeLabel,
            decimal OrderQuantity,
            decimal Revenue,
            decimal CostOfSales,
            decimal Profit)
        {
            public decimal MarginPercent => CalculateMarginPercent(Revenue, Profit);
        }

        // ============================ Chart ============================

        private static ExecutivePnLChartDto BuildProductTypeComposedChart(
            IReadOnlyList<ExecutivePnLDashboardPeriod> periods,
            IReadOnlyList<ExecutivePnLProductTypeMonthlyMetricRow> rows)
        {
            var selectedPeriod = periods.LastOrDefault();

            var periodRows = selectedPeriod is null
                ? rows
                : rows.Where(x => selectedPeriod.Contains(x.Year, x.Month)).ToList();

            var groups = periodRows
                .GroupBy(x => new { x.GroupKey, x.GroupLabel })
                .OrderByDescending(x => x.Sum(r => r.Revenue))
                .ToList();

            var productTypes = periodRows
                .GroupBy(x => new { x.ProductTypeKey, x.ProductTypeLabel })
                .OrderByDescending(x => x.Sum(r => r.Revenue))
                .ToList();

            return new ExecutivePnLChartDto
            {
                Code = "BY_PRODUCTTYPE_GROUP_COMPOSED",
                Title = "So sánh theo group và loại sản phẩm",
                Categories = groups.Select(x => x.Key.GroupLabel).ToList(),
                CategoryItems = groups.Select(x => new ExecutivePnLChartCategoryDto
                {
                    Key = x.Key.GroupKey,
                    Label = x.Key.GroupLabel
                }).ToList(),
                Series = productTypes.Select(pt => new ExecutivePnLChartSeriesDto
                {
                    Key = pt.Key.ProductTypeKey,
                    Name = pt.Key.ProductTypeLabel,
                    Type = "bar",
                    Data = groups
                        .Select(g => Math.Round(
                            g.Where(r => r.ProductTypeKey == pt.Key.ProductTypeKey)
                             .Sum(r => r.Profit), 2))
                        .ToList()
                }).ToList()
            };
        }


        //private static ExecutivePnLChartDto BuildProductTypeComposedChart(
        //    IReadOnlyList<ExecutivePnLDashboardPeriod> periods,
        //    IReadOnlyList<ExecutivePnLProductTypeMonthlyMetricRow> rows)
        //{
        //    var selectedPeriod = periods.LastOrDefault();

        //    var periodRows = selectedPeriod is null
        //        ? rows
        //        : rows
        //            .Where(x => selectedPeriod.Contains(x.Year, x.Month))
        //            .ToList();

        //    var groups = periodRows
        //        .GroupBy(x => new { x.GroupKey, x.GroupLabel })
        //        .OrderByDescending(x => x.Sum(r => r.Revenue))
        //        .ToList();

        //    return new ExecutivePnLChartDto
        //    {
        //        Code = "BY_PRODUCTTYPE_GROUP_COMPOSED",
        //        Title = "So sánh doanh thu và chỉ số theo loại sản phẩm",
        //        Categories = groups.Select(x => x.Key.GroupLabel).ToList(),
        //        CategoryItems = groups
        //            .Select(x => new ExecutivePnLChartCategoryDto
        //            {
        //                Key = x.Key.GroupKey,
        //                Label = x.Key.GroupLabel
        //            })
        //            .ToList(),
        //        Series = new List<ExecutivePnLChartSeriesDto>
        //        {
        //            BuildSeries("revenue", "Doanh thu", "bar", groups
        //                .Select(x => Math.Round(x.Sum(r => r.Revenue), 2))),

        //            BuildSeries("profit", "Lợi nhuận", "bar", groups
        //                .Select(x => Math.Round(x.Sum(r => r.Profit), 2))),

        //            BuildSeries("costOfSales", "Giá vốn", "bar", groups
        //                .Select(x => Math.Round(x.Sum(r => r.CostOfSales), 2))),

        //            BuildSeries("marginPercent", "Biên lợi nhuận", "line", groups
        //                .Select(x => CalculateMarginPercent(
        //                    x.Sum(r => r.Revenue),
        //                    x.Sum(r => r.Profit))))
        //        }
        //    };
        //}


        // ============================ 


        /// <summary>
        /// Dựng các section analysis cho tab loái ản phẩm với đầy đủ metric.
        /// </summary>
        private static List<ExecutivePnLAnalysisSectionDto> BuildProductTypeAnalysisSections(
            IReadOnlyList<ExecutivePnLDashboardPeriod> periods,
            IReadOnlyList<ExecutivePnLProductTypeMonthlyMetricRow> rows,
            int topN = 10)
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
                        BuildProductTypeAnalysisSummaryLine("TOTAL_SYSTEM", "Tổng", 10, periods, rows)
                    }
                });
            }

            var sortOrder = 20;


            sections.AddRange(rows
                .GroupBy(x => new { x.SaleKey, x.SaleLabel })
                .OrderBy(x => x.Key.SaleLabel)
                .Select(sale =>
                {
                    var section = new ExecutivePnLAnalysisSectionDto
                    {
                        Code = sale.Key.SaleKey,
                        Title = sale.Key.SaleLabel,
                        SortOrder = sortOrder,
                        Lines = BuildProductTypeAnalysisLines(periods, sale.ToList())
                    };

                    sortOrder += 10;
                    return section;
                //}).Take(topN)
                })
                );

            return sections;
        }

        /// <summary>
        /// Dựng dòng tổng tab analysis producttype tổng thì lấy hết.
        /// </summary>
        private static ExecutivePnLAnalysisLineDto BuildProductTypeAnalysisSummaryLine(
            string code,
            string label,
            int sortOrder,
            IReadOnlyList<ExecutivePnLDashboardPeriod> periods,
            IReadOnlyList<ExecutivePnLProductTypeMonthlyMetricRow> rows)
        {
            return new ExecutivePnLAnalysisLineDto
            {
                Code = code,
                Label = label,
                SortOrder = sortOrder,
                IsBold = true,
                IsSubtotal = true,
                MetricValues = BuildProductTypeMetricValues(periods, rows)
            };
        }

        /// <summary>
        /// Dung bo gia tri revenue, cost, profit va margin theo tung period cho tab sale.
        /// </summary>
        private static Dictionary<string, Dictionary<string, decimal>> BuildProductTypeMetricValues(
            IReadOnlyList<ExecutivePnLDashboardPeriod> periods,
            IReadOnlyList<ExecutivePnLProductTypeMonthlyMetricRow> rows)
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
        /// Dựng các dòng productType trong một loại sản phẩm và thêm dòng tổng.
        /// </summary>
        private static List<ExecutivePnLAnalysisLineDto> BuildProductTypeAnalysisLines(
            IReadOnlyList<ExecutivePnLDashboardPeriod> periods,
            IReadOnlyList<ExecutivePnLProductTypeMonthlyMetricRow> rows)
        {
            var sortOrder = 10;

            var lines = new List<ExecutivePnLAnalysisLineDto>
            {
                BuildProductTypeAnalysisSummaryLine("PRODUCTTYPE_TOTAL", "Tổng theo loại", sortOrder, periods, rows)
            };

            sortOrder += 10;

            lines.AddRange(rows
                .GroupBy(x => new { x.ProductTypeKey, x.ProductTypeLabel })
                .OrderByDescending(x => x.Sum(r => r.Revenue))
                .Select(productType =>
                {
                    var line = new ExecutivePnLAnalysisLineDto
                    {
                        Code = productType.Key.ProductTypeKey,
                        Label = productType.Key.ProductTypeLabel,
                        SortOrder = sortOrder,
                        MetricValues = BuildProductTypeMetricValues(periods, productType.ToList())
                    };

                    sortOrder += 10;
                    return line;
                }));

            return lines;
        }
    }
}
