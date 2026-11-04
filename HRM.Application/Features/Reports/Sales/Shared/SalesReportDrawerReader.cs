using HRM.Application.Abstractions.Persistence.Reports;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Reporting;
using HRM.Application.Commons.Reporting.SalesAttribution;
using HRM.Application.Features.Reports.Sales.Queries.GetSalesReportHeader;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Reports.Sales.Shared;

internal sealed class SalesReportDrawerReader(IReportReadDbContext db, ICurrentUser user)
{
    public async Task<List<DeliveryRevenueLine>> DeliveriesAsync(SalesReportDrawerRequest request,
        SalesReportPeriod period, CancellationToken ct)
    {
        var lines = await DeliveryRevenueQuery.Create(db.DeliveryOrderDetails.AsNoTracking(),
            period.From, period.ToExclusive, user.CompanyId!.Value,
            includeCosts: false, includeDisplayFields: false).ToListAsync(ct);
        var scope = await new SalesAttributionScopeResolver(db, user).ResolveAsync(user.CompanyId,
            lines.Select(x => x.CustomerId).Distinct().ToArray(), request.SaleGroup, request.SalePerson, ct, includeUnassignedCompanyCustomers: true);
        return lines.Where(x => scope.TryGetAttribution(x.CustomerId, out _)).ToList();
    }

    public async Task<Guid[]> VisibleCustomersAsync(IQueryable<HRM.Domain.Entities.OrderSchema.MerchandiseOrder> orders,
        SalesReportDrawerRequest request, CancellationToken ct)
    {
        var customers = await orders.Select(x => x.CustomerId).Distinct().ToArrayAsync(ct);
        var scope = await new SalesAttributionScopeResolver(db, user).ResolveAsync(user.CompanyId,
            customers, request.SaleGroup, request.SalePerson, ct, includeUnassignedCompanyCustomers: true);
        return customers.Where(x => scope.TryGetAttribution(x, out _)).ToArray();
    }

    public static SalesReportDrawerPage<T> Page<T>(SalesReportDrawerRequest request,
        SalesReportPeriod period, int count, IReadOnlyList<T> items)
        => new() { From = period.From, To = period.To, TotalCount = count,
            PageNumber = request.PageNumber, PageSize = request.PageSize, Items = items };
}

