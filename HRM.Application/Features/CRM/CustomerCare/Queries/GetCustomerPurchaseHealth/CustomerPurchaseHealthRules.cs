using HRM.Domain.Enums.Merchadises;

namespace HRM.Application.Features.CRM.CustomerCare.Queries.GetCustomerPurchaseHealth;

internal static class CustomerPurchaseHealthRules
{
    public static readonly string[] ExcludedOrderStatuses =
    [
        MerchadiseStatus.Cancelled.ToString(),
        "Canceled"
    ];
}
