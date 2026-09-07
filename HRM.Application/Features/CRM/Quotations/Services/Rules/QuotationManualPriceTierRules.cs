using HRM.Application.Commons.Pricing.Models;
using HRM.Application.Features.CRM.Quotations.Dtos;

namespace HRM.Application.Features.CRM.Quotations.Services;

internal static class QuotationManualPriceTierRules
{
    public static string? Validate(
        IReadOnlyList<QuotationLinePriceTierRequest> requestedTiers,
        FormulaPricingPolicyDefinition policy)
    {
        var activeTiers = requestedTiers
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ToArray();
        var policyTiers = policy.Tiers
            .OrderBy(x => x.SortOrder)
            .ToArray();

        if (activeTiers.Length != requestedTiers.Count ||
            activeTiers.Length != policyTiers.Length)
        {
            return "Manual authorized pricing must contain every active quantity range from the published pricing policy.";
        }

        for (var index = 0; index < policyTiers.Length; index++)
        {
            var requested = activeTiers[index];
            var expected = policyTiers[index];
            if (requested.SortOrder != expected.SortOrder ||
                requested.MinQuantity != expected.MinQuantity ||
                requested.MaxQuantity != expected.MaxQuantity ||
                requested.MinInclusive != expected.MinInclusive ||
                requested.MaxInclusive != expected.MaxInclusive)
            {
                return "Manual authorized pricing quantity ranges must match the published pricing policy.";
            }
        }

        return null;
    }
}
