using HRM.Domain.Enums.Merchadises;

namespace HRM.Application.Features.Reports.Sales.Queries.GetSalesReportHeader;

internal static class SalesPendingOrderRules
{
    public static readonly DateTime StartDate = new(2026, 3, 1);

    public static decimal Remaining(decimal ordered, decimal delivered) => Math.Max(0m, ordered - delivered);

    public static bool IsOverdue(DateTime requestedDate, DateTime today) => requestedDate.Date < today.Date;

    public static bool IsEligible(DateTime orderDate, string? status)
        => orderDate >= StartDate
            && status != MerchadiseStatus.Delivered.ToString()
            && status != MerchadiseStatus.Completed.ToString()
            && status != MerchadiseStatus.Cancelled.ToString();
}
