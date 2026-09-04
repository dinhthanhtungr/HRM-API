using HRM.Application.Abstractions.Persistence.Reports;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Reporting;
using HRM.Application.Features.Reports.ExecutivePnL.Shared.Models;
using HRM.Application.Features.Reports.ExecutivePnL.Shared.Services.Costing;
using HRM.Application.Features.Reports.ExecutivePnL.Shared.Services.SalesAttribution;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Reports.ExecutivePnL.Queries.GetExecutivePnLDashboardByProductType;

internal sealed class ExecutivePnLProductTypeReader
{
    private readonly IReportReadDbContext _dbContext;
    private readonly ExecutivePnLSalesAttributionScopeResolver _salesScopeResolver;

    public ExecutivePnLProductTypeReader(IReportReadDbContext dbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _salesScopeResolver = new ExecutivePnLSalesAttributionScopeResolver(dbContext, currentUser);
    }

    public async Task<List<ExecutivePnLProductTypeMonthlyMetricRow>> ReadMonthlyAsync(
        ExecutivePnLProductTypeFilter filter,
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

                return new ExecutivePnLProductTypeMonthlyMetricRow
                {
                    GroupKey = attribution.GroupKey,
                    GroupLabel = attribution.GroupLabel,
                    ProductKey = string.IsNullOrWhiteSpace(x.ProductKey) ? "UNKNOWN" : x.ProductKey,
                    ProductLabel = string.IsNullOrWhiteSpace(x.ProductName) ? "Chưa phân loại" : x.ProductName,
                    ProductTypeKey = string.IsNullOrWhiteSpace(x.ProductTypeKey) ? "UNKNOWN" : x.ProductTypeKey,
                    ProductTypeLabel = string.IsNullOrWhiteSpace(x.ProductTypeName) ? "Chưa phân loại" : x.ProductTypeName,
                    SaleKey = attribution.SaleId.ToString(),
                    SaleLabel = $"{attribution.GroupLabel} - {attribution.SaleLabel}",
                    Year = x.RevenueDate.Year,
                    Month = x.RevenueDate.Month,
                    OrderQuantity = x.Quantity,
                    Revenue = x.RevenueAmountVnd,
                    CostOfSales = costOfSales
                };
            });

        return rows
            .GroupBy(x => new
            {
                x.GroupKey,
                x.GroupLabel,
                x.ProductTypeKey,
                x.ProductTypeLabel,
                x.ProductLabel,
                x.ProductKey,
                x.SaleKey,
                x.SaleLabel,
                x.Year,
                x.Month
            })
            .Select(x => new ExecutivePnLProductTypeMonthlyMetricRow
            {
                GroupKey = x.Key.GroupKey,
                GroupLabel = x.Key.GroupLabel,
                ProductTypeKey = x.Key.ProductTypeKey,
                ProductTypeLabel = x.Key.ProductTypeLabel,
                ProductLabel = x.Key.ProductLabel,
                ProductKey = x.Key.ProductKey,
                SaleKey = x.Key.SaleKey,
                SaleLabel = x.Key.SaleLabel,
                Year = x.Key.Year,
                Month = x.Key.Month,
                OrderQuantity = x.Sum(r => r.OrderQuantity),
                Revenue = x.Sum(r => r.Revenue),
                CostOfSales = x.Sum(r => r.CostOfSales)
            })
            .ToList();
    }

}
