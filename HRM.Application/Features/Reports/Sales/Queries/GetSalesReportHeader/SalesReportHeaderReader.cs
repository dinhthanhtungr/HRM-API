using HRM.Application.Abstractions.Persistence.Reports;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Reporting;
using HRM.Application.Commons.Reporting.Costing;
using HRM.Application.Commons.Reporting.SalesAttribution;
using HRM.Domain.Enums.Merchadises;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Reports.Sales.Queries.GetSalesReportHeader;

internal sealed class SalesReportHeaderReader(IReportReadDbContext db, ICurrentUser currentUser)
{
    public async Task<SalesReportHeaderDto> ReadAsync(
        GetSalesReportHeaderQuery request, SalesReportPeriod period, Guid companyId,
        bool canViewProfit, DateTime today, CancellationToken ct)
    {
        var trendStart = SalesReportRevenueTrend.Start(period.To);
        var readFrom = trendStart < period.PreviousFrom ? trendStart : period.PreviousFrom;
        var revenueLines = await DeliveryRevenueQuery.Create(db.DeliveryOrderDetails.AsNoTracking(),
                readFrom, period.ToExclusive, companyId, includeCosts: false, includeDisplayFields: false)
            .ToListAsync(ct);
        var allOrderTypes = SalesReportOrderRules.Reportable(db.MerchandiseOrders.AsNoTracking(), companyId, period);
        var relevantOrders = allOrderTypes.Where(x => x.CreateDate >= period.From
            || (x.OrderType == OrderType.Merchandise && x.CreateDate >= SalesPendingOrderRules.StartDate
                && x.Status != MerchadiseStatus.Delivered.ToString()
                && x.Status != MerchadiseStatus.Completed.ToString()));
        var orderCustomers = await relevantOrders.Select(x => x.CustomerId).Distinct().ToListAsync(ct);
        var scope = await new SalesAttributionScopeResolver(db, currentUser).ResolveAsync(
            companyId, orderCustomers.Concat(revenueLines.Select(x => x.CustomerId)).Distinct().ToArray(),
            request.SaleGroup, request.SalePerson, ct, includeUnassignedCompanyCustomers: true);
        var visibleCustomerIds = orderCustomers.Where(x => scope.TryGetAttribution(x, out _)).ToArray();
        var scopedRevenue = revenueLines.Where(x => scope.TryGetAttribution(x.CustomerId, out _)).ToList();
        var currentLines = scopedRevenue.Where(x => x.RevenueDate >= period.From).ToList();
        var previousRevenue = scopedRevenue.Where(x => x.RevenueDate >= period.PreviousFrom
            && x.RevenueDate < period.From).Sum(x => x.RevenueAmountVnd);

        decimal? cost = null;
        if (canViewProfit)
        {
            var currentDetailIds = currentLines.Select(x => x.MerchandiseOrderDetailId).Distinct().ToArray();
            var costLines = await DeliveryRevenueQuery.Create(db.DeliveryOrderDetails.AsNoTracking(),
                    period.From, period.ToExclusive, companyId, includeDisplayFields: false)
                .Where(x => currentDetailIds.Contains(x.MerchandiseOrderDetailId)).ToListAsync(ct);
            var costs = await DeliveryCostResolver.LoadAsync(db, costLines, companyId, ct);
            cost = costLines.Sum(x => DeliveryCostResolver.ResolveAmount(x, costs));
        }

        var visibleOrders = relevantOrders.Where(x => x.OrderType == OrderType.Merchandise
            && visibleCustomerIds.Contains(x.CustomerId));
        var poRows = await allOrderTypes
            .Where(x => visibleCustomerIds.Contains(x.CustomerId) && x.CreateDate >= period.From)
            .GroupBy(x => new { x.OrderType, x.Status })
            .Select(g => new { Type = g.Key.OrderType, g.Key.Status, Count = g.Count() })
            .ToListAsync(ct);
        var poCounts = poRows.GroupBy(x => x.Type).ToDictionary(g => g.Key, g => g.Sum(x => x.Count));
        var completedPoCount = poRows.Where(x => SalesReportOrderRules.IsCompleted(x.Status)).Sum(x => x.Count);
        var newOrderCount = poCounts.GetValueOrDefault(OrderType.Merchandise);
        var details = await (
            from detail in db.MerchandiseOrderDetails.AsNoTracking()
            join order in visibleOrders on detail.MerchandiseOrderId equals order.MerchandiseOrderId
            where detail.IsActive
            select new SalesHeaderOrderLine
            {
                OrderId = order.MerchandiseOrderId,
                DetailId = detail.MerchandiseOrderDetailId,
                OrderDate = order.CreateDate,
                RequestedDate = detail.DeliveryRequestDate,
                ExpectedDeliveryDate = detail.ExpectedDeliveryDate,
                OrderStatus = order.Status,
                Quantity = detail.ExpectedQuantity,
                UnitPrice = detail.UnitPriceAgreed,
                TotalAmount = detail.TotalPriceAgreed,
                Currency = order.Currency,
                ExchangeRate = order.ExchangeRate
            }).ToListAsync(ct);
        var detailIds = details.Where(x => SalesPendingOrderRules.IsEligible(x.OrderDate, x.OrderStatus))
            .Select(x => x.DetailId).ToArray();
        var delivered = await DeliveryRevenueQuery.Create(db.DeliveryOrderDetails.AsNoTracking(),
                DateTime.MinValue, period.ToExclusive, companyId, includeCosts: false, includeDisplayFields: false)
            .Where(x => detailIds.Contains(x.MerchandiseOrderDetailId))
            .GroupBy(x => x.MerchandiseOrderDetailId)
            .Select(g => new { DetailId = g.Key, Quantity = g.Sum(x => x.Quantity) })
            .ToDictionaryAsync(x => x.DetailId, x => x.Quantity, ct);

        return SalesReportHeaderCalculator.Build(companyId, period, currentLines, previousRevenue,
            details, delivered, newOrderCount, cost, today,
            poCounts.GetValueOrDefault(OrderType.Complaint),
            poCounts.GetValueOrDefault(OrderType.SampleRequest), completedPoCount,
            SalesReportRevenueTrend.Build(period.To, scopedRevenue));
    }
}

internal sealed class SalesHeaderOrderLine
{
    public Guid OrderId { get; init; }
    public Guid DetailId { get; init; }
    public DateTime OrderDate { get; init; }
    public DateTime RequestedDate { get; init; }
    public DateTime? ExpectedDeliveryDate { get; init; }
    public string? OrderStatus { get; init; }
    public decimal Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal TotalAmount { get; init; }
    public string? Currency { get; init; }
    public decimal? ExchangeRate { get; init; }
}

