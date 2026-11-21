using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Features.CRM.Quotations.Services;

internal static class QuotationWorkflowRules
{
    public static bool CanRequestPricing(QuotationStatus status)
        => status == QuotationStatus.Draft;

    public static bool CanEditCustomerPricing(QuotationStatus status)
        => status is QuotationStatus.Draft or QuotationStatus.PendingApproval or QuotationStatus.Approved or QuotationStatus.PendingSendConfirmation or QuotationStatus.SendRejected;

    public static bool CanWithdrawPricingRequest(QuotationStatus status)
        => status is QuotationStatus.PendingApproval or QuotationStatus.Approved;

    public static DateTime NextUpdatedDate(DateTime now, DateTime? previous)
    {
        var rounded = new DateTime(now.Ticks - now.Ticks % TimeSpan.TicksPerMicrosecond, now.Kind);
        return previous.HasValue && rounded <= previous.Value
            ? new DateTime(previous.Value.Ticks - previous.Value.Ticks % TimeSpan.TicksPerMicrosecond, previous.Value.Kind)
                .AddTicks(TimeSpan.TicksPerMicrosecond)
            : rounded;
    }

    public static bool CanMarkSent(QuotationStatus status)
        => status is
            QuotationStatus.Draft or
            QuotationStatus.PendingApproval or
            QuotationStatus.Approved or QuotationStatus.SendRejected;
}
