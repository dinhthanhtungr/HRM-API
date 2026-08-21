using HRM.Application.Commons.Pricing.Dtos;
using HRM.Application.Commons.Pricing.Models;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Commons.Pricing.Helpers;

/// <summary>
/// Pure policy-based pricing calculations. This class never resolves configuration or supplies business defaults.
/// </summary>
public static class FormulaPriceCalculator
{
    private const int PercentScale = 4;

    public static IReadOnlyList<FormulaSuggestedPriceTierDto> BuildPriceTierTemplates(
        FormulaPricingPolicyDefinition policy)
        => policy.Tiers.OrderBy(x => x.SortOrder).Select(x => new FormulaSuggestedPriceTierDto
        {
            QuantityRangeLabel = x.QuantityRangeLabel,
            MinQuantity = x.MinQuantity,
            MaxQuantity = x.MaxQuantity,
            MinInclusive = x.MinInclusive,
            MaxInclusive = x.MaxInclusive,
            UnitPrice = null,
            MarginVsMaterialPercent = null,
            MarginVsCostPercent = null,
            RequiresManualPrice = !x.PriceOffset.HasValue,
            SortOrder = x.SortOrder
        }).ToArray();

    public static FormulaPriceCalculationDto Calculate(
        FormulaPricingPolicyDefinition policy,
        decimal materialCost,
        decimal? manufacturingCost,
        decimal? standardSellingPrice)
        => Calculate(policy, materialCost, manufacturingCost, standardSellingPrice, null, null);

    public static FormulaPriceCalculationDto Calculate(
        FormulaPricingPolicyDefinition policy,
        decimal materialCost,
        decimal? manufacturingCost,
        decimal? standardSellingPrice,
        decimal? profitMarginRate,
        ProductPricingChangedField? changedField)
    {
        Validate(policy, materialCost, manufacturingCost, standardSellingPrice, profitMarginRate, changedField);

        var roundedMaterialCost = Round(policy, materialCost);
        var usedDefaultManufacturingCost = manufacturingCost is null or <= 0m;
        var effectiveManufacturingCost = manufacturingCost is > 0m
            ? manufacturingCost.Value
            : policy.DefaultManufacturingCost;
        var costBase = Round(policy, roundedMaterialCost + effectiveManufacturingCost);
        var marginToApply = profitMarginRate ?? policy.DefaultProfitMarginRate;
        var resolvedStandardSellingPrice = changedField switch
        {
            ProductPricingChangedField.StandardSellingPrice when !standardSellingPrice.HasValue =>
                throw new ArgumentOutOfRangeException(nameof(standardSellingPrice)),
            ProductPricingChangedField.ProfitMarginRate when !profitMarginRate.HasValue =>
                throw new ArgumentOutOfRangeException(nameof(profitMarginRate)),
            ProductPricingChangedField.ProfitMarginRate or ProductPricingChangedField.ManufacturingCost =>
                Round(policy, costBase * (1m + marginToApply / 100m)),
            _ when standardSellingPrice.HasValue => PricingRoundingRules.RoundStoredInput(standardSellingPrice.Value),
            _ => Round(policy, costBase * (1m + marginToApply / 100m))
        };
        var resolvedProfitMarginRate = CalculateProfitMarginRate(
            resolvedStandardSellingPrice,
            costBase);
        if (resolvedProfitMarginRate is < 0m or > 100m)
            throw new ArgumentOutOfRangeException(nameof(standardSellingPrice));

        return new FormulaPriceCalculationDto
        {
            Profile = policy.Profile,
            MaterialCost = roundedMaterialCost,
            ManufacturingCost = effectiveManufacturingCost,
            UsedDefaultManufacturingCost = usedDefaultManufacturingCost,
            CostBase = costBase,
            StandardSellingPrice = resolvedStandardSellingPrice,
            ProfitMarginRate = resolvedProfitMarginRate,
            SuggestedPriceTiers = resolvedStandardSellingPrice > 0m
                ? policy.Tiers.OrderBy(x => x.SortOrder).Select(x => MapTier(
                    policy, x, resolvedStandardSellingPrice, materialCost, costBase)).ToArray()
                : []
        };
    }

    private static FormulaSuggestedPriceTierDto MapTier(
        FormulaPricingPolicyDefinition policy,
        FormulaPricingPolicyTierDefinition tier,
        decimal sellingPrice,
        decimal materialCost,
        decimal costBase)
    {
        var unitPrice = tier.PriceOffset.HasValue
            ? Round(policy, Math.Max(0m, sellingPrice + tier.PriceOffset.Value))
            : (decimal?)null;
        return new FormulaSuggestedPriceTierDto
        {
            QuantityRangeLabel = tier.QuantityRangeLabel,
            MinQuantity = tier.MinQuantity,
            MaxQuantity = tier.MaxQuantity,
            MinInclusive = tier.MinInclusive,
            MaxInclusive = tier.MaxInclusive,
            UnitPrice = unitPrice,
            MarginVsMaterialPercent = CalculateMarginPercent(unitPrice, materialCost),
            MarginVsCostPercent = CalculateMarginPercent(unitPrice, costBase),
            RequiresManualPrice = !unitPrice.HasValue,
            SortOrder = tier.SortOrder
        };
    }

    private static decimal Round(FormulaPricingPolicyDefinition policy, decimal value)
        => PricingRoundingRules.RoundCalculatedPrice(value, policy.RoundingRule, policy.RoundingIncrement);

    private static void Validate(
        FormulaPricingPolicyDefinition policy,
        decimal materialCost,
        decimal? manufacturingCost,
        decimal? standardSellingPrice,
        decimal? profitMarginRate,
        ProductPricingChangedField? changedField)
    {
        if (policy.RoundingIncrement <= 0m || !Enum.IsDefined(policy.RoundingRule) ||
            policy.DefaultManufacturingCost < 0m || policy.DefaultProfitMarginRate is < 0m or > 100m ||
            materialCost < 0m || manufacturingCost < 0m || standardSellingPrice < 0m ||
            profitMarginRate is < 0m or > 100m ||
            (changedField.HasValue && !Enum.IsDefined(changedField.Value)))
            throw new ArgumentOutOfRangeException(nameof(policy));
    }

    private static decimal? CalculateMarginPercent(decimal? price, decimal comparisonBase)
        => !price.HasValue || price <= 0m || comparisonBase <= 0m
            ? null
            : decimal.Round((price.Value - comparisonBase) / comparisonBase * 100m,
                PercentScale, MidpointRounding.AwayFromZero);

    private static decimal CalculateProfitMarginRate(decimal sellingPrice, decimal costBase)
        => costBase <= 0m
            ? 0m
            : decimal.Round((sellingPrice - costBase) / costBase * 100m,
                PercentScale, MidpointRounding.AwayFromZero);
}
