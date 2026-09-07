using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pricing.Models;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Features.CRM.Quotations.Services;

internal static class FormulaPricingPolicyRules
{
    public const string PricingPolicyMissing = "PricingPolicyMissing";

    public static string? ValidatePolicyConfiguration(
        string? name,
        string? currency,
        decimal defaultManufacturingCost,
        decimal defaultProfitMarginRate,
        FormulaPricingRoundingRule roundingRule,
        decimal roundingIncrement,
        DateTime effectiveFrom,
        int? priceValidityDays)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 150)
            return "Policy name is required and is limited to 150 characters.";
        if (!IsValidCurrency(currency))
            return "Currency must be a three-letter ISO currency code.";
        if (defaultManufacturingCost < 0m)
            return "Default manufacturing cost cannot be negative.";
        if (defaultProfitMarginRate is < 0m or > 100m)
            return "Default profit margin rate must be between 0 and 100.";
        if (!Enum.IsDefined(roundingRule) || roundingIncrement <= 0m)
            return "A valid rounding rule and positive rounding increment are required.";
        if (priceValidityDays is <= 0)
            return "PriceValidityDays must be greater than zero when provided.";
        return effectiveFrom == default
            ? "EffectiveFrom is required."
            : null;
    }

    public static bool IsValidCurrency(string? currency)
        => currency is { Length: 3 } && currency.All(char.IsAsciiLetter);

    public static int GetNextVersion(int latestVersion)
        => latestVersion < 0
            ? throw new ArgumentOutOfRangeException(nameof(latestVersion))
            : checked(latestVersion + 1);

    public static string? ValidatePublish(
        FormulaPricingPolicyStatus status,
        int version,
        DateTime? effectiveFrom,
        int tierCount)
    {
        if (status != FormulaPricingPolicyStatus.Draft || version <= 0 || tierCount == 0)
            return "Only a complete draft pricing policy can be published.";
        return effectiveFrom.HasValue && effectiveFrom.Value != default
            ? null
            : "EffectiveFrom is required before publishing a pricing policy.";
    }

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
                SortOrder = x.SortOrder,
                IsActive = x.IsActive
            }).ToArray());
    }

    public static FormulaPricingPolicyDefinition ToDefinition(FormulaPricingPolicy policy) => new(
        policy.Profile,
        policy.DefaultManufacturingCost,
        policy.DefaultProfitMarginRate,
        policy.RoundingRule,
        policy.RoundingIncrement,
        policy.Tiers.Where(x => x.IsActive).OrderBy(x => x.SortOrder).Select(x => new FormulaPricingPolicyTierDefinition(
            x.QuantityRangeLabel, x.MinQuantity, x.MaxQuantity, x.MinInclusive,
            x.MaxInclusive, x.PriceOffset, x.SortOrder)).ToArray());

    public static FormulaPricingPolicyDto ToDto(FormulaPricingPolicy policy) => new()
    {
        FormulaPricingPolicyId = policy.FormulaPricingPolicyId,
        CategoryId = policy.CategoryId,
        Profile = policy.Profile,
        Currency = policy.Currency,
        Name = policy.Name,
        Version = policy.Version,
        DefaultManufacturingCost = policy.DefaultManufacturingCost,
        DefaultProfitMarginRate = policy.DefaultProfitMarginRate,
        RoundingRule = policy.RoundingRule,
        RoundingIncrement = policy.RoundingIncrement,
        Status = policy.Status,
        EffectiveFrom = policy.EffectiveFrom,
        PriceValidityDays = policy.PriceValidityDays,
        PriceExpiresAt = policy.PriceValidityDays.HasValue && policy.EffectiveFrom.HasValue
            ? policy.EffectiveFrom.Value.AddDays(policy.PriceValidityDays.Value)
            : null,
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
            PriceOffset = x.PriceOffset ?? 0m,
            RequiresManualPrice = !x.PriceOffset.HasValue,
            IsActive = x.IsActive,
            SortOrder = x.SortOrder
        }).ToArray()
    };
}
