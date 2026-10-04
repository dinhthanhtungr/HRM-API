using HRM.Domain.Enums.Merchadises;

namespace HRM.Application.Features.Executive.MerchandiseOrderPriceHistory.Shared;

/// <summary>Canonical eligibility and ordering rules shared by Executive sale-price views.</summary>
internal static class MerchandiseOrderPriceHistoryRules
{
    internal static readonly OrderType[] EligibleOrderTypes =
    [
        OrderType.Merchandise,
        OrderType.SampleRequest,
        OrderType.Complaint
    ];

    internal static readonly string[] EligibleStatuses =
    [
        nameof(MerchadiseStatus.Approved),
        nameof(MerchadiseStatus.Processing),
        nameof(MerchadiseStatus.Delivering),
        nameof(MerchadiseStatus.Delivered),
        nameof(MerchadiseStatus.Completed)
    ];

    internal static bool IsEligibleStatus(string status) =>
        EligibleStatuses.Contains(status, StringComparer.OrdinalIgnoreCase);

    internal static bool IsEligibleOrderType(OrderType orderType) =>
        EligibleOrderTypes.Contains(orderType);
}
