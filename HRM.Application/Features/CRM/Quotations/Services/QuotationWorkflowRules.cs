using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Features.CRM.Quotations.Services;

internal static class QuotationWorkflowRules
{
    public static bool CanRequestPricing(QuotationStatus status)
        => status == QuotationStatus.Draft;

    public static bool CanEditCustomerPricing(QuotationStatus status)
        => status is QuotationStatus.PendingApproval or QuotationStatus.Approved;

    public static bool CanWithdrawPricingRequest(QuotationStatus status)
        => status is QuotationStatus.PendingApproval or QuotationStatus.Approved;

    public static bool CanMarkSent(QuotationStatus status)
        => status is
            QuotationStatus.Draft or
            QuotationStatus.PendingApproval or
            QuotationStatus.Approved;
}
