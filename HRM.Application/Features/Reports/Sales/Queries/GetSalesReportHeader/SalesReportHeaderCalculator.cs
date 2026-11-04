using HRM.Application.Commons.Reporting;
using HRM.Application.Commons.Reporting.SalesMetrics;
using HRM.Domain.Enums.Merchadises;

namespace HRM.Application.Features.Reports.Sales.Queries.GetSalesReportHeader;

internal static class SalesReportHeaderCalculator
{
    public static SalesReportHeaderDto Build(Guid companyId, SalesReportPeriod period,
        IReadOnlyList<DeliveryRevenueLine> currentLines, decimal previousRevenue,
        IReadOnlyList<SalesHeaderOrderLine> details, IReadOnlyDictionary<Guid, decimal> delivered,
        int newOrderCount, decimal? cost, DateTime today, int complaintPoCount = 0, int samplePoCount = 0,
        int completedPoCount = 0, IReadOnlyList<SalesReportMonthlyRevenueDto>? monthlyRevenue = null)
    {
        var revenue = currentLines.Sum(x => x.RevenueAmountVnd);
        var deliveredQuantity = currentLines.Sum(x => x.Quantity);
        var pending = details
            .Where(x => SalesPendingOrderRules.IsEligible(x.OrderDate, x.OrderStatus))
            .Select(x =>
        {
            delivered.TryGetValue(x.DetailId, out var quantity);
            var remaining = SalesPendingOrderRules.Remaining(x.Quantity, quantity);
            return new
            {
                x.OrderId, x.RequestedDate,
                ScheduledDate = x.ExpectedDeliveryDate ?? x.RequestedDate,
                x.OrderStatus,
                Quantity = remaining,
                Amount = remaining * x.UnitPrice * ReportMetricRules.ExchangeRate(x.Currency, x.ExchangeRate)
            };
        }).Where(x => x.Quantity > 0m).ToList();
        var profit = cost.HasValue ? revenue - cost.Value : (decimal?)null;
        var expectedPendingByMonth = pending
            .Where(x => x.ScheduledDate >= period.From && x.ScheduledDate < period.ToExclusive
                && x.OrderStatus != MerchadiseStatus.Completed.ToString()
                && x.OrderStatus != MerchadiseStatus.Delivered.ToString()
                && x.OrderStatus != MerchadiseStatus.Paused.ToString())
            .GroupBy(x => new DateTime(x.ScheduledDate.Year, x.ScheduledDate.Month, 1))
            .OrderBy(g => g.Key)
            .Select(g => new SalesReportExpectedPendingMonthDto(g.Key, g.Sum(x => x.Amount)))
            .ToList();
        var expectedPendingAmount = expectedPendingByMonth.Sum(x => x.Amount);
        return new SalesReportHeaderDto
        {
            CompanyId = companyId, From = period.From, To = period.To,
            PreviousFrom = period.PreviousFrom, PreviousTo = period.PreviousTo,
            Revenue = revenue, PreviousRevenue = previousRevenue,
            ExpectedPendingAmountInPeriod = expectedPendingAmount,
            ExpectedPendingByMonth = expectedPendingByMonth,
            ExpectedRevenueInPeriod = revenue + expectedPendingAmount,
            RevenueGrowthAmount = revenue - previousRevenue,
            RevenueGrowthPercent = ReportMetricRules.GrowthPercent(revenue, previousRevenue),
            DeliveredQuantity = deliveredQuantity,
            AverageSellingPrice = deliveredQuantity > 0m ? Math.Round(revenue / deliveredQuantity, 2) : null,
            CustomerCount = currentLines.Select(x => x.CustomerId).Distinct().Count(),
            DeliveredOrderCount = currentLines.Select(x => x.MerchandiseOrderId).Distinct().Count(),
            NewOrderCount = newOrderCount,
            MerchandisePoCount = newOrderCount,
            ComplaintPoCount = complaintPoCount,
            SamplePoCount = samplePoCount,
            CompletedPoCount = completedPoCount,
            IncompletePoCount = newOrderCount + complaintPoCount + samplePoCount - completedPoCount,
            MonthlyRevenue = monthlyRevenue ?? [],
            NewOrderAmount = details.Where(x => x.OrderDate >= period.From)
                .Sum(x => x.TotalAmount * ReportMetricRules.ExchangeRate(x.Currency, x.ExchangeRate)),
            PendingQuantity = pending.Sum(x => x.Quantity), PendingAmount = pending.Sum(x => x.Amount),
            PendingOrderCount = pending.Select(x => x.OrderId).Distinct().Count(),
            OverdueOrderCount = pending.Where(x => SalesPendingOrderRules.IsOverdue(x.RequestedDate, today))
                .Select(x => x.OrderId).Distinct().Count(),
            CanViewProfit = cost.HasValue, CostOfSales = cost, Profit = profit,
            MarginPercent = profit.HasValue ? ReportMetricRules.MarginPercent(revenue, profit.Value) : null
        };
    }
}
