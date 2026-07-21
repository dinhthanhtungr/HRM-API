using HRM.Application.Commons.Reporting;
using HRM.Domain.Enums.Formulas;
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
        var deliveryRows = await GetReportableDeliveryOrderDetails()
            .Where(x => x.IsActive
                && x.DeliveryOrder.IsActive
                && x.DeliveryOrder.CreatedDate.HasValue
                && x.DeliveryOrder.CreatedDate.Value >= filter.FromMonth
                && x.DeliveryOrder.CreatedDate.Value < filter.RangeEnd)
            .Where(x => !filter.CompanyId.HasValue || x.DeliveryOrder.CompanyId == filter.CompanyId.Value)
            .Where(x => x.MerchandiseOrderDetailId.HasValue)
            .Select(g => new
            {
                g.DeliveryOrder.CreatedDate,
                g.Quantity,
                g.LotNoList
            })
            .ToListAsync(cancellationToken);

        var lotCodes = deliveryRows
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

        var formulaCostMap = formulaCostRows
            .GroupBy(x => x.ExternalId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.First().FormulaCost,
                StringComparer.OrdinalIgnoreCase);

        foreach (var row in deliveryRows)
        {
            var createdDate = row.CreatedDate!.Value;

            if (!TryGetActual(actuals, createdDate.Year, createdDate.Month, out var actual))
            {
                continue;
            }

            // Formula cost = Sum(ManufacturingFormulaMaterials.TotalPrice).
            // Cost of sales = delivery quantity * formula unit cost.
            var formulaUnitCost = ExecutivePnLFormulaCostResolver.ResolveUnitCost(row.LotNoList, formulaCostMap);
            actual.CostOfSales += row.Quantity *
                ExecutivePnLAmountResolvers.ResolveManufacturingUnitCost(formulaUnitCost);
        }
    }
}

