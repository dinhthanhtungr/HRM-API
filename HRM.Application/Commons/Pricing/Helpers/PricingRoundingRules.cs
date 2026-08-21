namespace HRM.Application.Commons.Pricing.Helpers;

public static class PricingRoundingRules
{
    private const int StoredInputScale = 6;

    public static decimal RoundCalculatedPrice(decimal value)
        => decimal.Round(value, 0, MidpointRounding.AwayFromZero);

    public static decimal RoundStoredInput(decimal value)
        => decimal.Round(value, StoredInputScale, MidpointRounding.AwayFromZero);
}
