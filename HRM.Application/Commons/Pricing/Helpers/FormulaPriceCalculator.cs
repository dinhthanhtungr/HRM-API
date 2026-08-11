using HRM.Application.Commons.Pricing.Dtos;
using HRM.Domain.Enums.Formulas;

namespace HRM.Application.Commons.Pricing.Helpers;

/// <summary>
/// Tính giá bán gợi ý theo cùng một quy tắc cho PLM và báo giá.
/// Kết quả chỉ là preview; báo giá vẫn phải lưu snapshot giá được chọn.
/// </summary>
public static class FormulaPriceCalculator
{
    private const decimal DefaultPowderManufacturingCost = 10_000m;
    private const decimal DefaultCompoundManufacturingCost = 20_000m;
    private const int MoneyScale = 6;
    private const int PercentScale = 4;

    private static readonly PriceTierRule[] CompoundRules =
    [
        new("< 100 kg", null, 100m, true, false, 100_000m),
        new("100 - 300 kg", 100m, 300m, true, true, 50_000m),
        new("325 - 600 kg", 325m, 600m, true, true, 25_000m),
        new("625 kg - 1 tấn", 625m, 1_000m, true, true, 10_000m),
        new("1.025 - 5 tấn", 1_025m, 5_000m, true, true, 0m),
        new("5.025 - 10 tấn", 5_025m, 10_000m, true, true, -500m),
        new("> 10 tấn", 10_000m, null, false, true, -1_000m)
    ];

    private static readonly PriceTierRule[] PowderRules =
    [
        new("< 50 kg", null, 50m, true, false, 50_000m),
        new("50 - 99 kg", 50m, 99m, true, true, 20_000m),
        new("100 - 300 kg", 100m, 300m, true, true, 0m),
        new("325 - 600 kg", 325m, 600m, true, true, -1_000m),
        new("625 kg - 1 tấn", 625m, 1_000m, true, true, -2_000m),
        new("1.025 - 5 tấn", 1_025m, 5_000m, true, true, -3_000m),
        new("> 5 tấn", 5_000m, null, false, true, null)
    ];

    public static FormulaPriceCalculationDto Calculate(
        string? productCode,
        string? productAdditive,
        decimal materialCost,
        decimal? manufacturingCost,
        decimal? standardSellingPrice)
    {
        var profile = ResolveProfile(productCode, productAdditive);
        var usedDefaultManufacturingCost = manufacturingCost is null or <= 0m;
        var effectiveManufacturingCost = ResolveManufacturingCost(
            profile,
            manufacturingCost);
        var costBase = RoundMoney(materialCost + effectiveManufacturingCost);
        var resolvedStandardSellingPrice = standardSellingPrice.HasValue
            ? RoundMoney(standardSellingPrice.Value)
            : costBase;
        var profitMarginRate = CalculateProfitMarginRate(
            resolvedStandardSellingPrice,
            costBase);

        return new FormulaPriceCalculationDto
        {
            Profile = profile,
            MaterialCost = materialCost,
            ManufacturingCost = effectiveManufacturingCost,
            UsedDefaultManufacturingCost = usedDefaultManufacturingCost,
            CostBase = costBase,
            StandardSellingPrice = resolvedStandardSellingPrice,
            ProfitMarginRate = profitMarginRate,
            SuggestedPriceTiers = resolvedStandardSellingPrice > 0m
                ? BuildPriceTiers(
                    profile,
                    resolvedStandardSellingPrice,
                    materialCost,
                    costBase)
                : []
        };
    }

    public static decimal CalculateStandardSellingPrice(
        string? productCode,
        string? productAdditive,
        decimal materialCost,
        decimal? manufacturingCost,
        decimal profitMarginRate)
    {
        var effectiveManufacturingCost = ResolveManufacturingCost(
            productCode,
            productAdditive,
            manufacturingCost);
        var costBase = RoundMoney(materialCost + effectiveManufacturingCost);

        return RoundMoney(costBase * (1m + profitMarginRate / 100m));
    }

    public static decimal ResolveManufacturingCost(
        string? productCode,
        string? productAdditive,
        decimal? manufacturingCost)
    {
        return ResolveManufacturingCost(
            ResolveProfile(productCode, productAdditive),
            manufacturingCost);
    }

    private static FormulaPricingProfile ResolveProfile(
        string? productCode,
        string? productAdditive)
    {
        if (string.Equals(productAdditive?.Trim(), "C", StringComparison.OrdinalIgnoreCase))
        {
            return FormulaPricingProfile.Compound;
        }

        return productCode?.Trim().EndsWith("C", StringComparison.OrdinalIgnoreCase) == true
            ? FormulaPricingProfile.Compound
            : FormulaPricingProfile.Powder;
    }

    private static IReadOnlyList<FormulaSuggestedPriceTierDto> BuildPriceTiers(
        FormulaPricingProfile profile,
        decimal effectiveSellingPrice,
        decimal materialCost,
        decimal costBase)
    {
        var rules = profile == FormulaPricingProfile.Compound
            ? CompoundRules
            : PowderRules;

        return rules
            .Select((rule, index) =>
            {
                var unitPrice = rule.Offset.HasValue
                    ? RoundMoney(Math.Max(0m, effectiveSellingPrice + rule.Offset.Value))
                    : (decimal?)null;

                return new FormulaSuggestedPriceTierDto
                {
                    QuantityRangeLabel = rule.QuantityRangeLabel,
                    MinQuantity = rule.MinQuantity,
                    MaxQuantity = rule.MaxQuantity,
                    MinInclusive = rule.MinInclusive,
                    MaxInclusive = rule.MaxInclusive,
                    UnitPrice = unitPrice,
                    MarginVsMaterialPercent = CalculateMarginPercent(unitPrice, materialCost),
                    MarginVsCostPercent = CalculateMarginPercent(unitPrice, costBase),
                    RequiresManualPrice = !unitPrice.HasValue,
                    SortOrder = index
                };
            })
            .ToList();
    }

    private static decimal? CalculateMarginPercent(decimal? price, decimal comparisonBase)
    {
        if (!price.HasValue || price.Value <= 0m || comparisonBase <= 0m)
        {
            return null;
        }

        return decimal.Round(
            (price.Value - comparisonBase) / comparisonBase * 100m,
            PercentScale,
            MidpointRounding.AwayFromZero);
    }

    private static decimal CalculateProfitMarginRate(
        decimal standardSellingPrice,
        decimal costBase)
    {
        if (costBase <= 0m)
        {
            return 0m;
        }

        return decimal.Round(
            (standardSellingPrice - costBase) / costBase * 100m,
            PercentScale,
            MidpointRounding.AwayFromZero);
    }

    private static decimal ResolveManufacturingCost(
        FormulaPricingProfile profile,
        decimal? manufacturingCost)
    {
        return manufacturingCost is > 0m
            ? manufacturingCost.Value
            : profile == FormulaPricingProfile.Compound
                ? DefaultCompoundManufacturingCost
                : DefaultPowderManufacturingCost;
    }

    private static decimal RoundMoney(decimal value)
        => decimal.Round(value, MoneyScale, MidpointRounding.AwayFromZero);

    private sealed record PriceTierRule(
        string QuantityRangeLabel,
        decimal? MinQuantity,
        decimal? MaxQuantity,
        bool MinInclusive,
        bool MaxInclusive,
        decimal? Offset);
}
