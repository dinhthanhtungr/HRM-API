using HRM.Application.Commons.Concurrency;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pricing.Dtos;
using HRM.Application.Commons.Pricing.Helpers;
using HRM.Application.Commons.Pricing.Models;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.Formulas;

namespace HRM.Application.Features.CRM.Quotations.Services;

internal static class ProductPricingVersionPolicyRules
{
    public static OperationResult<ResolvedFormulaPricingPolicy> ResolveAttachedPolicy(
        ProductPricingVersion version,
        DateTime now)
    {
        if (!version.FormulaPricingPolicyId.HasValue || version.FormulaPricingPolicy is null)
        {
            return OperationResult<ResolvedFormulaPricingPolicy>.Fail(
                RebaseConflict("Legacy pricing versions without a policy are read-only"));
        }

        var policy = version.FormulaPricingPolicy;
        if (policy.Status != FormulaPricingPolicyStatus.Published ||
            !policy.IsActive ||
            !policy.EffectiveFrom.HasValue ||
            policy.EffectiveFrom > now)
        {
            return OperationResult<ResolvedFormulaPricingPolicy>.Fail(
                RebaseConflict("The attached pricing policy is no longer Published and effective"));
        }

        if (policy.CompanyId != version.CompanyId ||
            (policy.CategoryId.HasValue && policy.CategoryId != version.Product.CategoryId) ||
            policy.Profile != FormulaPricingProfileResolver.Resolve(
                version.Product.ColourCode,
                version.Product.Code,
                version.Product.Additive) ||
            !string.Equals(policy.Currency, version.Currency, StringComparison.OrdinalIgnoreCase))
        {
            return OperationResult<ResolvedFormulaPricingPolicy>.Fail(
                RebaseConflict("The attached pricing policy no longer matches company, profile, or currency"));
        }

        return OperationResult<ResolvedFormulaPricingPolicy>.Ok(
            new ResolvedFormulaPricingPolicy(
                policy.FormulaPricingPolicyId,
                policy.Version,
                FormulaPricingPolicyRules.ToDefinition(policy)));
    }

    public static OperationResult<FormulaPriceCalculationDto> Calculate(
        FormulaPricingPolicyDefinition policy,
        decimal? realtimeMaterialCost,
        decimal? manufacturingCost,
        decimal? standardSellingPrice,
        decimal? profitMarginRate,
        ProductPricingChangedField? changedField)
    {
        try
        {
            return OperationResult<FormulaPriceCalculationDto>.Ok(
                FormulaPriceCalculator.Calculate(
                    policy,
                    realtimeMaterialCost ?? 0m,
                    manufacturingCost,
                    standardSellingPrice,
                    profitMarginRate,
                    changedField));
        }
        catch (ArgumentOutOfRangeException)
        {
            return OperationResult<FormulaPriceCalculationDto>.Fail(
                "Pricing values or ChangedField are invalid for the attached policy.");
        }
    }

    public static OperationResult<PolicyTierBuildResult> BuildPolicyTiers(
        Guid productPricingVersionId,
        IReadOnlyList<FormulaSuggestedPriceTierDto> policyTiers,
        IReadOnlyList<ProductPricingTierRequest>? requestedTiers)
    {
        requestedTiers ??= [];
        var requestValidation = ProductPricingVersionRules.BuildTiers(
            productPricingVersionId,
            requestedTiers);
        if (!requestValidation.Success)
        {
            return OperationResult<PolicyTierBuildResult>.Fail(requestValidation.Message!);
        }

        var requestedBySortOrder = requestedTiers.ToDictionary(x => x.SortOrder);
        if (requestedBySortOrder.Keys.Any(sortOrder =>
                policyTiers.All(tier => tier.SortOrder != sortOrder)))
        {
            return OperationResult<PolicyTierBuildResult>.Fail(
                "PriceTiers contains a tier that is not defined by the pricing policy.");
        }

        var hasManualAdjustment = false;
        var finalRequests = new List<ProductPricingTierRequest>(policyTiers.Count);
        foreach (var policyTier in policyTiers.OrderBy(x => x.SortOrder))
        {
            requestedBySortOrder.TryGetValue(policyTier.SortOrder, out var requested);
            if (requested is not null && !HasSamePolicyRange(policyTier, requested))
            {
                return OperationResult<PolicyTierBuildResult>.Fail(
                    $"Price tier at SortOrder {policyTier.SortOrder} must keep the policy label and quantity range.");
            }

            var unitPrice = requested?.UnitPrice ?? policyTier.UnitPrice;
            if (!unitPrice.HasValue)
            {
                return OperationResult<PolicyTierBuildResult>.Fail(
                    $"A manual UnitPrice is required for policy tier '{policyTier.QuantityRangeLabel}'.");
            }

            hasManualAdjustment |= policyTier.RequiresManualPrice ||
                (requested is not null && requested.UnitPrice != policyTier.UnitPrice);
            finalRequests.Add(new ProductPricingTierRequest
            {
                QuantityRangeLabel = policyTier.QuantityRangeLabel,
                MinQuantity = policyTier.MinQuantity,
                MaxQuantity = policyTier.MaxQuantity,
                MinInclusive = policyTier.MinInclusive,
                MaxInclusive = policyTier.MaxInclusive,
                UnitPrice = unitPrice.Value,
                SortOrder = policyTier.SortOrder
            });
        }

        var tiersResult = ProductPricingVersionRules.BuildTiers(
            productPricingVersionId,
            finalRequests);
        return !tiersResult.Success || tiersResult.Data is null
            ? OperationResult<PolicyTierBuildResult>.Fail(tiersResult.Message!)
            : OperationResult<PolicyTierBuildResult>.Ok(
                new PolicyTierBuildResult(tiersResult.Data, hasManualAdjustment));
    }

    public static string RebaseConflict(string reason)
        => $"{reason}. Create a new pricing version using a current Published policy. " +
           OptimisticConcurrencyHelper.ConflictMessageMarker;

    private static bool HasSamePolicyRange(
        FormulaSuggestedPriceTierDto policyTier,
        ProductPricingTierRequest requested)
        => string.Equals(
               policyTier.QuantityRangeLabel.Trim(),
               requested.QuantityRangeLabel.Trim(),
               StringComparison.Ordinal) &&
           policyTier.MinQuantity == requested.MinQuantity &&
           policyTier.MaxQuantity == requested.MaxQuantity &&
           policyTier.MinInclusive == requested.MinInclusive &&
           policyTier.MaxInclusive == requested.MaxInclusive;
}

internal sealed record PolicyTierBuildResult(
    IReadOnlyList<ProductPricingTier> Tiers,
    bool HasManualTierAdjustment);
