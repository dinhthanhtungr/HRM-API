using HRM.Application.Abstractions.Persistence.Reports;
using HRM.Application.Commons.Reporting;
using HRM.Application.Features.Reports.ExecutivePnL.Queries.GetExecutivePnLReport.Services;
using HRM.Application.Features.Reports.ExecutivePnL.Shared.Models;
using HRM.Application.Features.Reports.ExecutivePnL.Shared.Services.Costing;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Reports.ExecutivePnL.Queries.GetExecutivePnLDashboardByCustomer;

internal sealed class ExecutivePnLCustomerReader
{
    private readonly IReportReadDbContext _dbContext;

    public ExecutivePnLCustomerReader(IReportReadDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<ExecutivePnLDashboardMetricRow>> ReadAsync(
        ExecutivePnLFilter filter,
        int topN,
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
                Key = x.CustomerId,
                Label = x.CustomerName
            })
            .Select(g => new ExecutivePnLDashboardMetricRow
            {
                Key = g.Key.Key.ToString(),
                Label = g.Key.Label,
                Revenue = g.Sum(x => x.RevenueAmountVnd),
                CostOfSales = g.Sum(x => ExecutivePnLDeliveryCostResolver.ResolveAmount(x, costSources))
            })
            .OrderByDescending(x => x.Revenue)
            .Take(topN)
            .ToList();
    }
}
