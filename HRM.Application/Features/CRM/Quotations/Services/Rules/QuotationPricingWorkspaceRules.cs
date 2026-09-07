using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Features.CRM.Quotations.Services;

internal static class QuotationPricingWorkspaceRules
{
    public static QuotationPricingWorkspaceState ResolveState(
        Guid? appliedProductPricingVersionId,
        int appliedTierCount,
        bool hasApprovedPricing,
        bool hasDraftPricing,
        bool hasEligibleSource)
    {
        if (appliedProductPricingVersionId.HasValue && appliedTierCount > 0)
        {
            return QuotationPricingWorkspaceState.Applied;
        }

        if (hasApprovedPricing)
        {
            return QuotationPricingWorkspaceState.ApprovedAvailable;
        }

        if (hasDraftPricing)
        {
            return QuotationPricingWorkspaceState.Draft;
        }

        return hasEligibleSource
            ? QuotationPricingWorkspaceState.WaitingForPricing
            : QuotationPricingWorkspaceState.NoEligibleSource;
    }
}
