using System.Globalization;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Commons.Pricing.Helpers;

/// <summary>
/// Tính giá bán từ giá thành và biên lợi nhuận trên giá bán.
/// Biên lợi nhuận được biểu diễn theo phần trăm: (giá bán - giá thành) / giá bán.
/// </summary>
public static class StandardSellingPriceCalculator
{
    public static StandardSellingPriceCalculationResult? TryCalculateFromCostComponents(
        decimal? materialCost,
        decimal? manufacturingCost,
        decimal? profitMarginRate,
        string currency)
    {
        if (materialCost is null or < 0m ||
            manufacturingCost is null or < 0m ||
            profitMarginRate is null or < 0m or >= 100m)
        {
            return null;
        }

        var costBase = PricingRoundingRules.RoundStoredInput(
            materialCost.Value + manufacturingCost.Value);
        var standardSellingPrice = CalculateFromProfitMarginOnSellingPrice(
            costBase,
            profitMarginRate.Value);
        return new StandardSellingPriceCalculationResult(
            materialCost.Value,
            manufacturingCost.Value,
            costBase,
            profitMarginRate.Value,
            standardSellingPrice,
            BuildDisplayFormula(
                materialCost.Value,
                manufacturingCost.Value,
                profitMarginRate.Value,
                standardSellingPrice,
                currency));
    }

    public static decimal? ResolveProfitMarginRateOnSellingPrice(
        decimal? storedProfitMarginRate,
        decimal? standardSellingPrice,
        decimal? materialCost,
        decimal? manufacturingCost)
    {
        if (storedProfitMarginRate.HasValue)
        {
            return storedProfitMarginRate is >= 0m and < 100m
                ? storedProfitMarginRate
                : null;
        }

        if (materialCost is null or < 0m || manufacturingCost is null or < 0m)
        {
            return null;
        }

        var costBase = PricingRoundingRules.RoundStoredInput(
            materialCost.Value + manufacturingCost.Value);
        return CalculateProfitMarginRateOnSellingPrice(standardSellingPrice, costBase);
    }

    public static decimal CalculateFromProfitMarginOnSellingPrice(
        decimal costBase,
        decimal profitMarginRate)
        => CalculateFromProfitMarginOnSellingPrice(
            costBase,
            profitMarginRate,
            PricingRoundingRules.RoundCalculatedPrice);

    public static decimal CalculateFromProfitMarginOnSellingPrice(
        decimal costBase,
        decimal profitMarginRate,
        FormulaPricingRoundingRule roundingRule,
        decimal roundingIncrement)
        => CalculateFromProfitMarginOnSellingPrice(
            costBase,
            profitMarginRate,
            value => PricingRoundingRules.RoundCalculatedPrice(value, roundingRule, roundingIncrement));

    private static decimal CalculateFromProfitMarginOnSellingPrice(
        decimal costBase,
        decimal profitMarginRate,
        Func<decimal, decimal> round)
    {
        if (costBase < 0m || profitMarginRate is < 0m or >= 100m)
        {
            throw new ArgumentOutOfRangeException(nameof(profitMarginRate));
        }

        return round(costBase / (1m - profitMarginRate / 100m));
    }

    public static decimal? CalculateProfitMarginRateOnSellingPrice(
        decimal? standardSellingPrice,
        decimal? costBase)
        => standardSellingPrice is > 0m && costBase.HasValue
            ? decimal.Round(
                (standardSellingPrice.Value - costBase.Value) / standardSellingPrice.Value * 100m,
                4,
                MidpointRounding.AwayFromZero)
            : null;

    private static string BuildDisplayFormula(
        decimal materialCost,
        decimal manufacturingCost,
        decimal profitMarginRate,
        decimal standardSellingPrice,
        string currency)
    {
        var profitMarginRatio = profitMarginRate / 100m;
        return $"({Format(materialCost)} + {Format(manufacturingCost)}) / " +
               $"(1 - {Format(profitMarginRatio)}) = {Format(standardSellingPrice)} {currency}";
    }

    private static string Format(decimal value)
        => value.ToString("0.######", CultureInfo.InvariantCulture);
}

public sealed record StandardSellingPriceCalculationResult(
    decimal MaterialCost,
    decimal ManufacturingCost,
    decimal CostBase,
    decimal ProfitMarginRate,
    decimal StandardSellingPrice,
    string DisplayFormula);
