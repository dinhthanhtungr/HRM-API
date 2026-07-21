using HRM.Application.Features.Reports.ExecutivePnL.Shared.Dtos;
using HRM.Application.Features.Reports.ExecutivePnL.Queries.GetExecutivePnLReport.Models;

namespace HRM.Application.Features.Reports.ExecutivePnL.Queries.GetExecutivePnLReport.Services
{
    internal sealed class ExecutivePnLReportBuilder
    {
        private const string TotalKey = "total_actual";

        public ExecutivePnLReportDto Build(
            ExecutivePnLFilter filter,
            IReadOnlyList<DateTime> months,
            IReadOnlyDictionary<string, ExecutivePnLMonthlyActual> actuals)
        {
            var total = BuildTotal(actuals);

            return new ExecutivePnLReportDto
            {
                CompanyName = filter.CompanyId?.ToString() ?? "VIETAUS POLYMER",
                BusinessUnit = string.IsNullOrWhiteSpace(filter.BusinessUnit)
                    ? "ALL BUS"
                    : filter.BusinessUnit,
                Currency = string.IsNullOrWhiteSpace(filter.Currency)
                    ? "VND"
                    : filter.Currency,
                FromMonth = filter.FromMonth,
                ToMonth = filter.ToMonth,
                Columns = BuildColumns(months),
                Sections = BuildSections(months, actuals, total)
            };
        }

        private static List<ExecutivePnLSectionDto> BuildSections(
            IReadOnlyList<DateTime> months,
            IReadOnlyDictionary<string, ExecutivePnLMonthlyActual> actuals,
            ExecutivePnLMonthlyActual total)
        {
            return new List<ExecutivePnLSectionDto>
            {
                new()
                {
                    Code = "SALES",
                    Title = "Sales",
                    SortOrder = 10,
                    Lines = new List<ExecutivePnLLineDto>
                    {
                        BuildLine("SALES_TONNES", "Sales Tonnes", 10, months, actuals, total, x => x.SalesTonnes),
                        BuildLine("SALES_REVENUE", "Sales revenue", 20, months, actuals, total, x => x.SalesRevenue, isBold: true),
                        //BuildLine("SALES_DEDUCTIONS", "Sales deductions", 30, months, actuals, total, x => x.SalesDeductions),
                        BuildLine("NET_SALES", "Net sales", 40, months, actuals, total, x => x.NetSales, isBold: true, isSubtotal: true)
                    }
                },
                new()
                {
                    Code = "COST",
                    Title = "Cost",
                    SortOrder = 20,
                    Lines = new List<ExecutivePnLLineDto>
                    {
                        BuildLine("COST_OF_SALES", "Cost of sales", 10, months, actuals, total, x => x.CostOfSales),
                        BuildLine("ELECTRICITY_COST", "Electricity cost", 15, months, actuals, total, x => x.ElectricityCost),
                        BuildLine("GROSS_MARGIN", "Gross margin", 20, months, actuals, total, ExecutivePnLCalculator.GrossMargin, isBold: true, isSubtotal: true)
                    }
                },
                new()
                {
                    Code = "SELLING_EXPENSE",
                    Title = "Selling expense",
                    SortOrder = 30,
                    Lines = new List<ExecutivePnLLineDto>
                    {
                        BuildLine("FREIGHT", "Freight", 10, months, actuals, total, x => x.FreightAmount)
                    }
                },
                new()
                {
                    Code = "MARGIN",
                    Title = "Margin",
                    SortOrder = 40,
                    Lines = new List<ExecutivePnLLineDto>
                    {
                        BuildLine("GROSS_MARGIN_PERCENT", "Gross margin %", 10, months, actuals, total, ExecutivePnLCalculator.GrossMarginPercent)
                    }
                },
                new()
                {
                    Code = "OPERATIONS",
                    Title = "Operations",
                    SortOrder = 50,
                    Lines = new List<ExecutivePnLLineDto>
                    {
                        BuildLine("DELIVERED_QTY", "Delivered quantity", 10, months, actuals, total, x => x.DeliveredQuantity),
                        BuildLine("PRODUCTION_QTY", "Production quantity", 20, months, actuals, total, x => x.ProductionQuantity),
                        BuildLine("PRODUCTION_ORDER_COUNT", "Production order count", 30, months, actuals, total, x => x.ProductionOrderCount)
                    }
                }
            };
        }

        private static ExecutivePnLLineDto BuildLine(
            string code,
            string label,
            int sortOrder,
            IReadOnlyList<DateTime> months,
            IReadOnlyDictionary<string, ExecutivePnLMonthlyActual> actuals,
            ExecutivePnLMonthlyActual total,
            Func<ExecutivePnLMonthlyActual, decimal> valueSelector,
            bool isBold = false,
            bool isSubtotal = false)
        {
            var values = new Dictionary<string, decimal>();

            foreach (var month in months)
            {
                var key = ExecutivePnLPeriod.MonthKey(month.Year, month.Month);
                values[key] = Math.Round(valueSelector(actuals[key]), 2);
            }

            values[TotalKey] = Math.Round(valueSelector(total), 2);

            return new ExecutivePnLLineDto
            {
                Code = code,
                Label = label,
                SortOrder = sortOrder,
                IsBold = isBold,
                IsSubtotal = isSubtotal,
                Values = values
            };
        }

        private static List<ExecutivePnLColumnDto> BuildColumns(IReadOnlyList<DateTime> months)
        {
            var columns = months
                .Select(x => new ExecutivePnLColumnDto
                {
                    Key = ExecutivePnLPeriod.MonthKey(x.Year, x.Month),
                    Label = x.ToString("yyyy-MM"),
                    ColumnType = "Actual"
                })
                .ToList();

            columns.Add(new ExecutivePnLColumnDto
            {
                Key = TotalKey,
                Label = "Total actual",
                ColumnType = "ActualTotal"
            });

            return columns;
        }

        private static ExecutivePnLMonthlyActual BuildTotal(
            IReadOnlyDictionary<string, ExecutivePnLMonthlyActual> actuals)
        {
            return new ExecutivePnLMonthlyActual
            {
                SalesTonnes = actuals.Values.Sum(x => x.SalesTonnes),
                SalesRevenue = actuals.Values.Sum(x => x.SalesRevenue),
                SalesDeductions = actuals.Values.Sum(x => x.SalesDeductions),
                NetSales = actuals.Values.Sum(x => x.NetSales),
                CostOfSales = actuals.Values.Sum(x => x.CostOfSales),
                ElectricityCost = actuals.Values.Sum(x => x.ElectricityCost),
                FreightAmount = actuals.Values.Sum(x => x.FreightAmount),
                OrderCount = actuals.Values.Sum(x => x.OrderCount),
                OrderLineCount = actuals.Values.Sum(x => x.OrderLineCount),
                DeliveredQuantity = actuals.Values.Sum(x => x.DeliveredQuantity),
                ProductionQuantity = actuals.Values.Sum(x => x.ProductionQuantity),
                ProductionOrderCount = actuals.Values.Sum(x => x.ProductionOrderCount)
            };
        }
    }
}

