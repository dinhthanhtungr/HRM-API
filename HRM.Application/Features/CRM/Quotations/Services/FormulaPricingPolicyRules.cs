using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pricing.Models;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Domain.Entities.CustomerSchema;

namespace HRM.Application.Features.CRM.Quotations.Services;

internal static class FormulaPricingPolicyRules
{
    public static OperationResult<IReadOnlyList<FormulaPricingPolicyTier>> BuildTiers(
        Guid policyId,
        IReadOnlyList<FormulaPricingPolicyTierRequest> requests)
    {
        if (requests.Count == 0)
            return OperationResult<IReadOnlyList<FormulaPricingPolicyTier>>.Fail("At least one pricing tier is required.");
        if (requests.GroupBy(x => x.SortOrder).Any(x => x.Count() > 1))
            return OperationResult<IReadOnlyList<FormulaPricingPolicyTier>>.Fail("Pricing tier sort order must be unique.");

        foreach (var tier in requests)
        {
            if (string.IsNullOrWhiteSpace(tier.QuantityRangeLabel) || tier.QuantityRangeLabel.Trim().Length > 50)
                return OperationResult<IReadOnlyList<FormulaPricingPolicyTier>>.Fail("Each pricing tier requires a label up to 50 characters.");
            if (tier.MinQuantity < 0m || tier.MaxQuantity < 0m)
                return OperationResult<IReadOnlyList<FormulaPricingPolicyTier>>.Fail("Tier quantities cannot be negative.");
            if (tier.MinQuantity.HasValue && tier.MaxQuantity.HasValue && tier.MinQuantity >= tier.MaxQuantity)
                return OperationResult<IReadOnlyList<FormulaPricingPolicyTier>>.Fail("Tier minimum quantity must be less than maximum quantity.");
        }

        var ordered = requests.OrderBy(x => x.MinQuantity ?? decimal.MinValue).ThenBy(x => x.SortOrder).ToArray();
        for (var index = 1; index < ordered.Length; index++)
        {
            var previous = ordered[index - 1];
            var current = ordered[index];
            if (!previous.MaxQuantity.HasValue || !current.MinQuantity.HasValue ||
                previous.MaxQuantity > current.MinQuantity ||
                (previous.MaxQuantity == current.MinQuantity && previous.MaxInclusive && current.MinInclusive))
                return OperationResult<IReadOnlyList<FormulaPricingPolicyTier>>.Fail("Pricing tier quantity ranges cannot overlap.");
        }

        return OperationResult<IReadOnlyList<FormulaPricingPolicyTier>>.Ok(requests
            .OrderBy(x => x.SortOrder)
            .Select(x => new FormulaPricingPolicyTier
            {
                FormulaPricingPolicyTierId = Guid.CreateVersion7(),
                FormulaPricingPolicyId = policyId,
                QuantityRangeLabel = x.QuantityRangeLabel.Trim(),
                MinQuantity = x.MinQuantity,
                MaxQuantity = x.MaxQuantity,
                MinInclusive = x.MinInclusive,
                MaxInclusive = x.MaxInclusive,
                PriceOffset = x.PriceOffset,
                SortOrder = x.SortOrder
            }).ToArray());
    }

    public static FormulaPricingPolicyDefinition ToDefinition(FormulaPricingPolicy policy) => new(
        policy.Profile,
        policy.DefaultManufacturingCost,
        policy.Tiers.OrderBy(x => x.SortOrder).Select(x => new FormulaPricingPolicyTierDefinition(
            x.QuantityRangeLabel, x.MinQuantity, x.MaxQuantity, x.MinInclusive,
            x.MaxInclusive, x.PriceOffset, x.SortOrder)).ToArray());

    public static FormulaPricingPolicyDto ToDto(FormulaPricingPolicy policy) => new()
    {
        FormulaPricingPolicyId = policy.FormulaPricingPolicyId,
        Profile = policy.Profile,
        Currency = policy.Currency,
        Name = policy.Name,
        Version = policy.Version,
        DefaultManufacturingCost = policy.DefaultManufacturingCost,
        Status = policy.Status,
        EffectiveFrom = policy.EffectiveFrom,
        PublishedAt = policy.PublishedAt,
        UpdatedDate = policy.UpdatedDate,
        Tiers = policy.Tiers.OrderBy(x => x.SortOrder).Select(x => new FormulaPricingPolicyTierDto
        {
            FormulaPricingPolicyTierId = x.FormulaPricingPolicyTierId,
            QuantityRangeLabel = x.QuantityRangeLabel,
            MinQuantity = x.MinQuantity,
            MaxQuantity = x.MaxQuantity,
            MinInclusive = x.MinInclusive,
            MaxInclusive = x.MaxInclusive,
            PriceOffset = x.PriceOffset,
            SortOrder = x.SortOrder
        }).ToArray()
    };
}
