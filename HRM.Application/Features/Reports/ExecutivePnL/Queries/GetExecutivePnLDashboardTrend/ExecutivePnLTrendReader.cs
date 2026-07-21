using HRM.Application.Abstractions.Persistence.Reports;
using HRM.Application.Commons.Reporting;
using HRM.Application.Features.Reports.ExecutivePnL.Queries.GetExecutivePnLReport.Services;
using HRM.Application.Features.Reports.ExecutivePnL.Shared.Models;
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
        return await DeliveryRevenueQuery.Create(
                _dbContext.DeliveryOrderDetails.AsNoTracking(),
                filter.FromMonth,
                filter.RangeEnd,
                filter.CompanyId)
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
                CostOfSales = g.Sum(x => x.BaseCostAmount)
            })
            .OrderBy(x => x.Year)
            .ThenBy(x => x.Month)
            .ToListAsync(cancellationToken);
    }
}
