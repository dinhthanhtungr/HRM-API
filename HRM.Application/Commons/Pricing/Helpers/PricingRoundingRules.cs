using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Commons.Pricing.Helpers;

public static class PricingRoundingRules
{
    private const int StoredInputScale = 6;

    public static decimal RoundCalculatedPrice(decimal value)
        => decimal.Round(value, 0, MidpointRounding.AwayFromZero);

    public static decimal RoundCalculatedPrice(
        decimal value,
        FormulaPricingRoundingRule roundingRule,
        decimal roundingIncrement)
    {
        if (roundingIncrement <= 0m)
            throw new ArgumentOutOfRangeException(nameof(roundingIncrement));

        var units = value / roundingIncrement;
        var roundedUnits = roundingRule switch
        {
            FormulaPricingRoundingRule.Nearest => decimal.Round(
                units, 0, MidpointRounding.AwayFromZero),
            FormulaPricingRoundingRule.Up => decimal.Ceiling(units),
            FormulaPricingRoundingRule.Down => decimal.Floor(units),
            _ => throw new ArgumentOutOfRangeException(nameof(roundingRule))
        };
        return roundedUnits * roundingIncrement;
    }

    public static decimal RoundStoredInput(decimal value)
        => decimal.Round(value, StoredInputScale, MidpointRounding.AwayFromZero);
}
