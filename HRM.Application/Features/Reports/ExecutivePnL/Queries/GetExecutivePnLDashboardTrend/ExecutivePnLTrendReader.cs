using HRM.Application.Abstractions.Persistence.Reports;
using HRM.Application.Commons.Reporting;
using HRM.Application.Features.Reports.ExecutivePnL.Queries.GetExecutivePnLReport.Services;
using HRM.Application.Features.Reports.ExecutivePnL.Shared.Models;
using HRM.Application.Features.Reports.ExecutivePnL.Shared.Services.Costing;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Reports.ExecutivePnL.Queries.GetExecutivePnLDashboardTrend;

internal sealed class ExecutivePnLTrendReader
{
    private readonly IReportReadDbContext _dbContext;

    public ExecutivePnLTrendReader(IReportReadDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Đọc xu hướng doanh số và giá vốn theo tháng từ nguồn doanh số chuẩn.
    /// </summary>
    public async Task<List<ExecutivePnLTrendMetricRow>> ReadAsync(
        ExecutivePnLFilter filter,
        CancellationToken cancellationToken)
    {
        var revenueLines = await DeliveryRevenueQuery.Create(
                _dbContext.DeliveryOrderDetails.AsNoTracking(),
                filter.FromMonth,
                filter.RangeEnd,
                filter.CompanyId)
            .ToListAsync(cancellationToken);

        var costSources = await ExecutivePnLDeliveryCostResolver.LoadAsync(
            _dbContext,
            revenueLines,
            filter.CompanyId,
            cancellationToken);

        return revenueLines
            .GroupBy(x => new
            {
                x.RevenueDate.Year,
                x.RevenueDate.Month
            })
            .Select(g => new ExecutivePnLTrendMetricRow
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                Revenue = g.Sum(x => x.RevenueAmountVnd),
                CostOfSales = g.Sum(x => ExecutivePnLDeliveryCostResolver.ResolveAmount(x, costSources))
            })
            .OrderBy(x => x.Year)
            .ThenBy(x => x.Month)
            .ToList();
    }
}
