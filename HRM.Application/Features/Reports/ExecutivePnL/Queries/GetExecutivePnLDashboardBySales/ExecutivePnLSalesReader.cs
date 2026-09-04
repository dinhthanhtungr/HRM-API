using HRM.Application.Abstractions.Persistence.Reports;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Reporting;
using HRM.Application.Features.Reports.ExecutivePnL.Shared.Models;
using HRM.Application.Features.Reports.ExecutivePnL.Shared.Services.Costing;
using HRM.Application.Features.Reports.ExecutivePnL.Shared.Services.SalesAttribution;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Reports.ExecutivePnL.Queries.GetExecutivePnLDashboardBySales;

internal sealed class ExecutivePnLSalesReader
{
    private readonly IReportReadDbContext _dbContext;
    private readonly ExecutivePnLSalesAttributionScopeResolver _salesScopeResolver;

    public ExecutivePnLSalesReader(IReportReadDbContext dbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _salesScopeResolver = new ExecutivePnLSalesAttributionScopeResolver(dbContext, currentUser);
    }

    /// <summary>
    /// Đọc doanh số chuẩn theo tháng và phân bổ customer cho sale/group trong scope hiện tại.
    /// </summary>
    public async Task<List<ExecutivePnLSalesMonthlyMetricRow>> ReadMonthlyAsync(
        ExecutivePnLSaleFilter filter,
        CancellationToken cancellationToken = default)
    {
        var revenueLines = await DeliveryRevenueQuery.Create(
                _dbContext.DeliveryOrderDetails.AsNoTracking(),
                filter.FromMonth,
                filter.RangeEnd,
                filter.CompanyId)
            .ToListAsync(cancellationToken);

        var customerIds = revenueLines
            .Select(x => x.CustomerId)
            .Distinct()
            .ToList();

        var salesScope = await _salesScopeResolver.ResolveAsync(
            filter.CompanyId,
            customerIds,
            filter.SaleGroup,
            filter.SalePerson,
            cancellationToken);

        var costSources = await ExecutivePnLDeliveryCostResolver.LoadAsync(
            _dbContext,
            revenueLines,
            filter.CompanyId,
            cancellationToken);

        var rows = revenueLines
            .Where(x => salesScope.TryGetAttribution(x.CustomerId, out _))
            .Select(x =>
            {
                salesScope.TryGetAttribution(x.CustomerId, out var attribution);

                var costOfSales = ExecutivePnLDeliveryCostResolver.ResolveAmount(x, costSources);

                return new ExecutivePnLSalesMonthlyMetricRow
                {
                    GroupKey = attribution.GroupKey,
                    GroupLabel = attribution.GroupLabel,
                    SaleKey = attribution.SaleId.ToString(),
                    SaleLabel = attribution.SaleLabel,
                    Year = x.RevenueDate.Year,
                    Month = x.RevenueDate.Month,
                    Revenue = x.RevenueAmountVnd,
                    CostOfSales = costOfSales
                };
            });

        return rows
            .GroupBy(x => new
            {
                x.GroupKey,
                x.GroupLabel,
                x.SaleKey,
                x.SaleLabel,
                x.Year,
                x.Month
            })
            .Select(x => new ExecutivePnLSalesMonthlyMetricRow
            {
                GroupKey = x.Key.GroupKey,
                GroupLabel = x.Key.GroupLabel,
                SaleKey = x.Key.SaleKey,
                SaleLabel = x.Key.SaleLabel,
                Year = x.Key.Year,
                Month = x.Key.Month,
                Revenue = x.Sum(r => r.Revenue),
                CostOfSales = x.Sum(r => r.CostOfSales)
            })
            .ToList();
    }

}
