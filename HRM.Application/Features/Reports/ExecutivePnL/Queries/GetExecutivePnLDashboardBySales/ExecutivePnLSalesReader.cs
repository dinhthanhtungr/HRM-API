using HRM.Application.Abstractions.Persistence.Reports;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Reporting;
using HRM.Application.Features.Reports.ExecutivePnL.Queries.GetExecutivePnLReport.Services;
using HRM.Application.Features.Reports.ExecutivePnL.Shared.Models;
using HRM.Application.Features.Reports.ExecutivePnL.Shared.Services.Costing;
using HRM.Application.Features.Reports.ExecutivePnL.Shared.Services.SalesAttribution;
using HRM.Domain.Enums.Formulas;
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

        var formulaCostMap = await LoadFormulaCostMapAsync(revenueLines, cancellationToken);

        var rows = revenueLines
            .Where(x => salesScope.TryGetAttribution(x.CustomerId, out _))
            .Select(x =>
            {
                salesScope.TryGetAttribution(x.CustomerId, out var attribution);

                var formulaUnitCost = ExecutivePnLFormulaCostResolver.ResolveUnitCost(
                    x.LotNoList,
                    formulaCostMap);

                return new ExecutivePnLSalesMonthlyMetricRow
                {
                    GroupKey = attribution.GroupKey,
                    GroupLabel = attribution.GroupLabel,
                    SaleKey = attribution.SaleId.ToString(),
                    SaleLabel = attribution.SaleLabel,
                    Year = x.RevenueDate.Year,
                    Month = x.RevenueDate.Month,
                    Revenue = x.RevenueAmountVnd,
                    CostOfSales = x.Quantity
                        * ExecutivePnLAmountResolvers.ResolveManufacturingUnitCost(formulaUnitCost)
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

    private async Task<Dictionary<string, decimal>> LoadFormulaCostMapAsync(
        IReadOnlyCollection<DeliveryRevenueLine> revenueLines,
        CancellationToken cancellationToken)
    {
        var lotCodes = revenueLines
            .SelectMany(x => ExecutivePnLFormulaCostResolver.SplitLotCodes(x.LotNoList))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var formulaCostRows = await _dbContext.ManufacturingFormulas
            .AsNoTracking()
            .Where(x => x.IsActive && lotCodes.Contains(x.ExternalId))
            .Select(x => new
            {
                x.ExternalId,
                FormulaCost = x.ManufacturingFormulaMaterials
                    .Where(m => m.IsActive && m.itemType == ItemType.Material)
                    .Sum(m => m.TotalPrice)
            })
            .ToListAsync(cancellationToken);

        return formulaCostRows
            .GroupBy(x => x.ExternalId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                x => x.Key,
                x => x.First().FormulaCost,
                StringComparer.OrdinalIgnoreCase);
    }
}
