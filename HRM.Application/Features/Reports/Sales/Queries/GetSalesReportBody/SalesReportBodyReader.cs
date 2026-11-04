using HRM.Application.Abstractions.Persistence.Reports;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Reporting;
using HRM.Application.Commons.Reporting.Costing;
using HRM.Application.Commons.Reporting.SalesAttribution;
using HRM.Application.Features.Reports.Sales.Queries.GetSalesReportHeader;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Reports.Sales.Queries.GetSalesReportBody;

internal sealed class SalesReportBodyReader(IReportReadDbContext db, ICurrentUser user)
{
    public async Task<IReadOnlyList<SalesReportBodyLine>> ReadAsync(GetSalesReportBodyQuery request,
        SalesReportPeriod period, Guid companyId, bool canViewProfit, CancellationToken ct)
    {
        var lines = await DeliveryRevenueQuery.Create(db.DeliveryOrderDetails.AsNoTracking(),
            period.From, period.ToExclusive, companyId, canViewProfit).ToListAsync(ct);
        var scope = await new SalesAttributionScopeResolver(db, user).ResolveAsync(companyId,
            lines.Select(x => x.CustomerId).Distinct().ToArray(), request.SaleGroup, request.SalePerson, ct, includeUnassignedCompanyCustomers: true);
        lines = lines.Where(x => scope.TryGetAttribution(x.CustomerId, out _)).ToList();
        var costs = canViewProfit ? await DeliveryCostResolver.LoadAsync(db, lines, companyId, ct) : null;
        var customerTypes = new Dictionary<Guid, string?>();
        if (request.GroupBy == SalesReportDimension.CustomerType)
        {
            var orderIds = lines.Select(x => x.MerchandiseOrderId).Distinct().ToArray();
            customerTypes = await db.MerchandiseOrders.AsNoTracking()
                .Where(x => x.CompanyId == companyId && orderIds.Contains(x.MerchandiseOrderId))
                .Select(x => new { x.MerchandiseOrderId, Type = x.Customer == null ? null : x.Customer.CustomerGroup })
                .ToDictionaryAsync(x => x.MerchandiseOrderId, x => x.Type, ct);
        }
        return lines.Select(line =>
        {
            scope.TryGetAttribution(line.CustomerId, out var sale);
            var customerType = customerTypes.GetValueOrDefault(line.MerchandiseOrderId);
            var (groupKey, groupLabel, key, label) = request.GroupBy switch
            {
                SalesReportDimension.Sale => (sale.GroupKey, sale.GroupLabel, sale.SaleId.ToString(), sale.SaleLabel),
                SalesReportDimension.Product => (KnownKey(line.ProductTypeKey), KnownLabel(line.ProductTypeName),
                    KnownKey(line.ProductKey), KnownLabel(line.ProductName)),
                _ => ("CUSTOMER_TYPES", "Loại khách hàng", KnownKey(customerType), KnownLabel(customerType))
            };
            return new SalesReportBodyLine(groupKey, groupLabel, key, label, line.RevenueAmountVnd,
                line.Quantity, costs is null ? null : DeliveryCostResolver.ResolveAmount(line, costs));
        }).ToList();
    }

    private static string KnownKey(string? value) => string.IsNullOrWhiteSpace(value) ? "UNKNOWN" : value.Trim();
    private static string KnownLabel(string? value) => string.IsNullOrWhiteSpace(value) ? "Chưa phân loại" : value.Trim();
}

