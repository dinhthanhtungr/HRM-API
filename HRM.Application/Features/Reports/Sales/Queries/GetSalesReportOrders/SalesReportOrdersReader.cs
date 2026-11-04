using HRM.Application.Abstractions.Persistence.Reports;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Reporting;
using HRM.Application.Features.Reports.Sales.Queries.GetSalesReportHeader;
using HRM.Application.Features.Reports.Sales.Shared;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Enums.Merchadises;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Reports.Sales.Queries.GetSalesReportOrders;

internal sealed class SalesReportOrdersReader(IReportReadDbContext db, ICurrentUser user)
{
    public static bool IsValidMetric(string? metric) => metric is "completedPo" or "incompletePo"
        or "pendingOrder" or "overdueOrder" or "deliveredOrder"
        or "merchandisePo" or "complaintPo" or "samplePo";

    public async Task<SalesReportDrawerPage<SalesReportOrderDto>> ReadAsync(
        GetSalesReportOrdersQuery request, SalesReportPeriod period, DateTime today, CancellationToken ct)
    {
        var helper = new SalesReportDrawerReader(db, user);
        var orders = db.MerchandiseOrders.AsNoTracking();
        Dictionary<Guid, decimal>? deliveryAmounts = null;
        Dictionary<Guid, PendingSummary>? pending = null;
        if (request.Metric == "deliveredOrder")
        {
            var deliveries = await helper.DeliveriesAsync(request, period, ct);
            deliveryAmounts = deliveries.GroupBy(x => x.MerchandiseOrderId)
                .ToDictionary(x => x.Key, x => x.Sum(l => l.Quantity));
            var ids = deliveryAmounts.Keys.ToArray();
            orders = orders.Where(x => ids.Contains(x.MerchandiseOrderId));
        }
        else
        {
            orders = SalesReportOrderRules.Reportable(orders, user.CompanyId!.Value, period);
            if (request.Metric is "merchandisePo" or "complaintPo" or "samplePo")
            {
                var orderType = request.Metric switch
                {
                    "merchandisePo" => OrderType.Merchandise,
                    "complaintPo" => OrderType.Complaint,
                    _ => OrderType.SampleRequest
                };
                orders = orders.Where(x => x.CreateDate >= period.From && x.OrderType == orderType);
            }
            else if (request.Metric is "completedPo" or "incompletePo")
            {
                orders = orders.Where(x => x.CreateDate >= period.From);
                orders = request.Metric == "completedPo"
                    ? orders.Where(x => x.Status == MerchadiseStatus.Completed.ToString()
                        || x.Status == MerchadiseStatus.Delivered.ToString())
                    : orders.Where(x => x.Status != MerchadiseStatus.Completed.ToString()
                        && x.Status != MerchadiseStatus.Delivered.ToString());
            }
            else
            {
                orders = orders.Where(x => x.OrderType == OrderType.Merchandise
                    && x.CreateDate >= SalesPendingOrderRules.StartDate
                    && x.Status != MerchadiseStatus.Completed.ToString()
                    && x.Status != MerchadiseStatus.Delivered.ToString());
            }
            var customers = await helper.VisibleCustomersAsync(orders, request, ct);
            orders = orders.Where(x => customers.Contains(x.CustomerId));
            if (request.Metric is "pendingOrder" or "overdueOrder")
            {
                pending = await PendingAsync(orders, period, today, request.Metric == "overdueOrder", ct);
                var ids = pending.Keys.ToArray();
                orders = orders.Where(x => ids.Contains(x.MerchandiseOrderId));
            }
        }
        var count = await orders.CountAsync(ct);
        var items = await orders.OrderByDescending(x => x.CreateDate).ThenBy(x => x.MerchandiseOrderId)
            .Skip((request.PageNumber - 1) * request.PageSize).Take(request.PageSize)
            .Select(x => new SalesReportOrderDto
            {
                OrderId = x.MerchandiseOrderId, ExternalId = x.ExternalId, PONo = x.PONo,
                CustomerId = x.CustomerId, CustomerCode = x.CustomerExternalIdSnapshot,
                CustomerName = x.CustomerNameSnapshot, OrderType = x.OrderType,
                Status = x.Status, CreatedDate = x.CreateDate,
                TotalQuantity = x.MerchandiseOrderDetails.Where(d => d.IsActive)
                    .Sum(d => (decimal?)d.ExpectedQuantity) ?? 0m
            }).ToListAsync(ct);
        var pageOrderIds = items.Select(x => x.OrderId).ToArray();
        var totalDelivered = new Dictionary<Guid, decimal>();
        var promisedDates = new Dictionary<Guid, DateTime?>();
        if (pageOrderIds.Length > 0)
        {
            var activeDetailIds = db.MerchandiseOrderDetails.AsNoTracking()
                .Where(x => x.IsActive && pageOrderIds.Contains(x.MerchandiseOrderId))
                .Select(x => x.MerchandiseOrderDetailId);
            var promisedDetails = db.MerchandiseOrderDetails.AsNoTracking()
                .Where(x => x.IsActive && pageOrderIds.Contains(x.MerchandiseOrderId));
            if (pending is not null)
            {
                var pendingDetailIds = pending.Values.SelectMany(x => x.DetailIds).ToArray();
                promisedDetails = promisedDetails.Where(x => pendingDetailIds.Contains(x.MerchandiseOrderDetailId));
            }
            promisedDates = await (from detail in promisedDetails
                join link in db.MfgOrderPOs.AsNoTracking() on detail.MerchandiseOrderDetailId equals link.MerchandiseOrderDetailId
                join production in db.MfgProductionOrders.AsNoTracking() on link.MfgProductionOrderId equals production.MfgProductionOrderId
                where link.IsActive && production.IsActive && production.CompanyId == user.CompanyId
                    && production.ExpectedDate != null
                group production by detail.MerchandiseOrderId into dates
                select new { OrderId = dates.Key, Date = dates.Min(x => x.ExpectedDate) })
                .ToDictionaryAsync(x => x.OrderId, x => x.Date, ct);
            totalDelivered = await DeliveryRevenueQuery.Create(db.DeliveryOrderDetails.AsNoTracking(),
                    DateTime.MinValue, period.ToExclusive, user.CompanyId!.Value,
                    includeCosts: false, includeDisplayFields: false)
                .Where(x => pageOrderIds.Contains(x.MerchandiseOrderId)
                    && activeDetailIds.Contains(x.MerchandiseOrderDetailId))
                .GroupBy(x => x.MerchandiseOrderId)
                .Select(g => new { Id = g.Key, Quantity = g.Sum(x => x.Quantity) })
                .ToDictionaryAsync(x => x.Id, x => x.Quantity, ct);
        }
        foreach (var item in items)
        {
            item.PromisedDeliveryDate = promisedDates.GetValueOrDefault(item.OrderId);
            item.DeliveredQuantity = totalDelivered.GetValueOrDefault(item.OrderId);
            if (deliveryAmounts is not null)
                item.DeliveredQuantityInPeriod = deliveryAmounts[item.OrderId];
            if (pending is not null)
            {
                item.PendingQuantity = pending[item.OrderId].Quantity;
                item.EarliestPendingRequestedDate = pending[item.OrderId].RequestedDate;
            }
        }
        return SalesReportDrawerReader.Page(request, period, count, items);
    }

    private async Task<Dictionary<Guid, PendingSummary>> PendingAsync(IQueryable<MerchandiseOrder> orders,
        SalesReportPeriod period, DateTime today, bool overdueOnly, CancellationToken ct)
    {
        var details = await (from detail in db.MerchandiseOrderDetails.AsNoTracking()
            join order in orders on detail.MerchandiseOrderId equals order.MerchandiseOrderId
            where detail.IsActive
            select new { detail.MerchandiseOrderId, detail.MerchandiseOrderDetailId,
                detail.ExpectedQuantity, detail.DeliveryRequestDate }).ToListAsync(ct);
        var ids = details.Select(x => x.MerchandiseOrderDetailId).ToArray();
        var delivered = await DeliveryRevenueQuery.Create(db.DeliveryOrderDetails.AsNoTracking(),
                DateTime.MinValue, period.ToExclusive, user.CompanyId!.Value,
                includeCosts: false, includeDisplayFields: false)
            .Where(x => ids.Contains(x.MerchandiseOrderDetailId))
            .GroupBy(x => x.MerchandiseOrderDetailId)
            .Select(g => new { Id = g.Key, Quantity = g.Sum(x => x.Quantity) })
            .ToDictionaryAsync(x => x.Id, x => x.Quantity, ct);
        return details.Select(x => new { x.MerchandiseOrderId, x.MerchandiseOrderDetailId, x.DeliveryRequestDate,
                Quantity = SalesPendingOrderRules.Remaining(x.ExpectedQuantity,
                    delivered.GetValueOrDefault(x.MerchandiseOrderDetailId)) })
            .Where(x => x.Quantity > 0).GroupBy(x => x.MerchandiseOrderId)
            .Where(g => !overdueOnly || g.Any(x => SalesPendingOrderRules.IsOverdue(x.DeliveryRequestDate, today)))
            .ToDictionary(g => g.Key, g => new PendingSummary(g.Sum(x => x.Quantity),
                g.Min(x => x.DeliveryRequestDate), g.Select(x => x.MerchandiseOrderDetailId).ToArray()));
    }

    private sealed record PendingSummary(decimal Quantity, DateTime RequestedDate, Guid[] DetailIds);
}
