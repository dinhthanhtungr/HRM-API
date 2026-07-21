using HRM.Domain.Entities.DeliverySchema;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Enums.Manufacturings;
using HRM.Domain.Enums.Merchadises;
using Microsoft.EntityFrameworkCore;
using HRM.Application.Features.Reports.ExecutivePnL.Queries.GetExecutivePnLReport.Models;

namespace HRM.Application.Features.Reports.ExecutivePnL.Queries.GetExecutivePnLReport.Services;

internal sealed partial class ExecutivePnLActualReader
{
    private async Task AddOrderCountsAsync(
        Dictionary<string, ExecutivePnLMonthlyActual> actuals,
        ExecutivePnLFilter filter,
        CancellationToken cancellationToken)
    {
        var rows = await GetReportableMerchandiseOrders()
            .Where(x => x.IsActive
                && x.CreateDate >= filter.FromMonth
                && x.CreateDate < filter.RangeEnd)
            .Where(x => !filter.CompanyId.HasValue || x.CompanyId == filter.CompanyId.Value)
            .GroupBy(x => new
            {
                x.CreateDate.Year,
                x.CreateDate.Month
            })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Count = g.Count()
            })
            .ToListAsync(cancellationToken);

        foreach (var row in rows)
        {
            if (TryGetActual(actuals, row.Year, row.Month, out var actual))
            {
                actual.OrderCount += row.Count;
            }
        }
    }

    private async Task AddDeliveryQuantitiesAsync(
        Dictionary<string, ExecutivePnLMonthlyActual> actuals,
        ExecutivePnLFilter filter,
        CancellationToken cancellationToken)
    {
        var rows = await GetReportableDeliveryOrderDetails()
            .Where(x => x.IsActive
                && x.DeliveryOrder.IsActive
                && x.DeliveryOrder.CreatedDate.HasValue
                && x.DeliveryOrder.CreatedDate.Value >= filter.FromMonth
                && x.DeliveryOrder.CreatedDate.Value < filter.RangeEnd)
            .Where(x => !filter.CompanyId.HasValue || x.DeliveryOrder.CompanyId == filter.CompanyId.Value)
            .GroupBy(x => new
            {
                x.DeliveryOrder.CreatedDate!.Value.Year,
                x.DeliveryOrder.CreatedDate!.Value.Month
            })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Quantity = g.Sum(x => x.Quantity)
            })
            .ToListAsync(cancellationToken);

        foreach (var row in rows)
        {
            if (TryGetActual(actuals, row.Year, row.Month, out var actual))
            {
                actual.DeliveredQuantity += row.Quantity;
                actual.SalesTonnes = actual.DeliveredQuantity;
                actual.NetSales = actual.SalesRevenue - actual.SalesDeductions;
            }
        }
    }

    private async Task AddFreightAmountsAsync(
        Dictionary<string, ExecutivePnLMonthlyActual> actuals,
        ExecutivePnLFilter filter,
        CancellationToken cancellationToken)
    {
        var rows = await GetReportableDeliveryOrders()
            .Where(x => x.IsActive
                && x.CreatedDate.HasValue
                && x.CreatedDate.Value >= filter.FromMonth
                && x.CreatedDate.Value < filter.RangeEnd)
            .Where(x => !filter.CompanyId.HasValue || x.CompanyId == filter.CompanyId.Value)
            .GroupBy(x => new
            {
                x.CreatedDate!.Value.Year,
                x.CreatedDate!.Value.Month
            })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                FreightAmount = g.Sum(x => x.DeliveryPrice ?? 0)
            })
            .ToListAsync(cancellationToken);

        foreach (var row in rows)
        {
            if (TryGetActual(actuals, row.Year, row.Month, out var actual))
            {
                actual.FreightAmount += row.FreightAmount;
            }
        }
    }

    private async Task AddProductionActualsAsync(
        Dictionary<string, ExecutivePnLMonthlyActual> actuals,
        ExecutivePnLFilter filter,
        CancellationToken cancellationToken)
    {
        var rows = await GetReportableProductionOrders()
            .Where(x => x.IsActive
                && (x.ManufacturingDate ?? x.CreatedDate) >= filter.FromMonth
                && (x.ManufacturingDate ?? x.CreatedDate) < filter.RangeEnd)
            .Where(x => !filter.CompanyId.HasValue || x.CompanyId == filter.CompanyId.Value)
            .GroupBy(x => new
            {
                (x.ManufacturingDate ?? x.CreatedDate).Year,
                (x.ManufacturingDate ?? x.CreatedDate).Month
            })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Quantity = g.Sum(x => x.TotalQuantity ?? x.TotalQuantityRequest),
                Count = g.Count()
            })
            .ToListAsync(cancellationToken);

        foreach (var row in rows)
        {
            if (!TryGetActual(actuals, row.Year, row.Month, out var actual))
            {
                continue;
            }

            actual.ProductionQuantity += row.Quantity;
            actual.ProductionOrderCount += row.Count;
        }
    }
}

