using HRM.Application.Commons.Reporting;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Enums.Merchadises;

namespace HRM.Application.Features.Reports.Sales.Queries.GetSalesReportHeader;

internal static class SalesReportOrderRules
{
    public static IQueryable<MerchandiseOrder> Reportable(IQueryable<MerchandiseOrder> orders,
        Guid companyId, SalesReportPeriod period)
        => orders.Where(x => x.CompanyId == companyId && x.IsActive
            && (x.OrderType == OrderType.Merchandise || x.OrderType == OrderType.Complaint
                || x.OrderType == OrderType.SampleRequest)
            && x.Status != MerchadiseStatus.Cancelled.ToString()
            && x.CustomerExternalIdSnapshot != DeliveryRevenueQuery.InternalCustomerExternalId
            && x.CreateDate < period.ToExclusive);

    public static bool IsCompleted(string? status)
        => status == MerchadiseStatus.Completed.ToString() || status == MerchadiseStatus.Delivered.ToString();
}
