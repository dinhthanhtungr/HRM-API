using HRM.Application.Commons.Reporting;
using Microsoft.EntityFrameworkCore;
using HRM.Application.Features.Reports.ExecutivePnL.Queries.GetExecutivePnLReport.Models;
using HRM.Application.Features.Reports.ExecutivePnL.Shared.Services.Costing;

namespace HRM.Application.Features.Reports.ExecutivePnL.Queries.GetExecutivePnLReport.Services;

internal sealed partial class ExecutivePnLActualReader
{
    /// <summary>
    /// Thêm doanh số từ nguồn chuẩn DeliveryOrderDetail theo ngày tạo phiếu giao hàng.
    /// </summary>
    private async Task AddSalesActualsAsync(
        Dictionary<string, ExecutivePnLMonthlyActual> actuals,
        ExecutivePnLFilter filter,
        CancellationToken cancellationToken)
    {
        var rows = await DeliveryRevenueQuery.Create(
                _dbContext.DeliveryOrderDetails.AsNoTracking(),
                filter.FromMonth,
                filter.RangeEnd,
                filter.CompanyId)
            .GroupBy(x => new
            {
                x.RevenueDate.Year,
                x.RevenueDate.Month
            })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Revenue = g.Sum(x => x.RevenueAmountVnd),
                OrderLineCount = g.Count()
            })
            .ToListAsync(cancellationToken);

        foreach (var row in rows)
        {
            if (!TryGetActual(actuals, row.Year, row.Month, out var actual))
            {
                continue;
            }

            actual.SalesRevenue += row.Revenue;
            actual.OrderLineCount += row.OrderLineCount;
        }
    }

    private static void ApplySalesDeductions(
        Dictionary<string, ExecutivePnLMonthlyActual> actuals)
    {
        foreach (var actual in actuals.Values)
        {
            actual.SalesDeductions += ExecutivePnLAmountResolvers.ResolveSalesDeductionAmount();
            actual.NetSales = actual.SalesRevenue - actual.SalesDeductions;
        }
    }

    private async Task AddCostOfSalesAsync(
        Dictionary<string, ExecutivePnLMonthlyActual> actuals,
        ExecutivePnLFilter filter,
        CancellationToken cancellationToken)
    {
        var deliveryRows = await DeliveryRevenueQuery.Create(
                _dbContext.DeliveryOrderDetails.AsNoTracking(),
                filter.FromMonth,
                filter.RangeEnd,
                filter.CompanyId)
            .ToListAsync(cancellationToken);

        var costSources = await ExecutivePnLDeliveryCostResolver.LoadAsync(
            _dbContext,
            deliveryRows,
            filter.CompanyId,
            cancellationToken);

        foreach (var row in deliveryRows)
        {
            var createdDate = row.RevenueDate;

            if (!TryGetActual(actuals, createdDate.Year, createdDate.Month, out var actual))
            {
                continue;
            }

            actual.CostOfSales += ExecutivePnLDeliveryCostResolver.ResolveAmount(row, costSources);
        }
    }
}

