using HRM.Application.Abstractions.Persistence.Reports;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.Reports.Sales.Queries.GetSalesReportHeader;
using HRM.Application.Features.Reports.Sales.Shared;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Reports.Sales.Queries.GetSalesReportCustomers;

internal sealed class SalesReportCustomersReader(IReportReadDbContext db, ICurrentUser user)
{
    public async Task<SalesReportDrawerPage<SalesReportCustomerDto>> ReadAsync(
        GetSalesReportCustomersQuery request, SalesReportPeriod period, CancellationToken ct)
    {
        var lines = await new SalesReportDrawerReader(db, user).DeliveriesAsync(request, period, ct);
        var groups = lines.GroupBy(x => x.CustomerId).OrderBy(x => x.Key).ToList();
        var page = groups.Skip((request.PageNumber - 1) * request.PageSize).Take(request.PageSize).ToList();
        var orderIds = page.SelectMany(x => x.Select(l => l.MerchandiseOrderId)).Distinct().ToArray();
        // Nhãn snapshot của PO mới nhất có giao trong kỳ; không đọc thông tin liên hệ CRM.
        var labels = await db.MerchandiseOrders.AsNoTracking()
            .Where(x => orderIds.Contains(x.MerchandiseOrderId))
            .Select(x => new { x.MerchandiseOrderId, x.CustomerId, x.CreateDate,
                x.CustomerExternalIdSnapshot, x.CustomerNameSnapshot }).ToListAsync(ct);
        var labelMap = labels.GroupBy(x => x.CustomerId).ToDictionary(x => x.Key,
            x => x.OrderByDescending(o => o.CreateDate).ThenBy(o => o.MerchandiseOrderId).First());
        var items = page.Select(g => new SalesReportCustomerDto
        {
            CustomerId = g.Key,
            CustomerCode = labelMap.TryGetValue(g.Key, out var label) ? label.CustomerExternalIdSnapshot : string.Empty,
            CustomerName = labelMap.TryGetValue(g.Key, out label) ? label.CustomerNameSnapshot : string.Empty,
            DeliveredOrderCount = g.Select(x => x.MerchandiseOrderId).Distinct().Count(),
            DeliveredQuantityInPeriod = g.Sum(x => x.Quantity)
        }).ToList();
        return SalesReportDrawerReader.Page(request, period, groups.Count, items);
    }
}

