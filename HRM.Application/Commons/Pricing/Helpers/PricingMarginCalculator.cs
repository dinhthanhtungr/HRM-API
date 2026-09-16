namespace HRM.Application.Commons.Pricing.Helpers;

/// <summary>
/// Canonical profit-margin calculation shared by pricing API projections.
/// </summary>
public static class PricingMarginCalculator
{
    public static decimal? CalculateProfitMarginPercent(
        decimal? sellingPrice,
        decimal? costBase)
        => !sellingPrice.HasValue || !costBase.HasValue || sellingPrice <= 0m
            ? null
            : decimal.Round(
                (sellingPrice.Value - costBase.Value) / sellingPrice.Value * 100m,
                4,
                MidpointRounding.AwayFromZero);
}
