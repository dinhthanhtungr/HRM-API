namespace HRM.Application.Features.PLM.Materials.Services;

internal static class MaterialSupplierManagementRules
{
    internal const decimal MaxSupportedPrice = 99_999_999_999_999.9999m;
    internal const int MaxCurrencyLength = 10;
    internal const int MaxDeliveryDays = 3650;

    internal static string? ValidatePrice(decimal? price, bool required, string fieldName)
    {
        if (required && !price.HasValue)
            return $"{fieldName} is required.";
        if (price is < 0m)
            return $"{fieldName} cannot be negative.";
        if (price > MaxSupportedPrice)
            return $"{fieldName} cannot exceed {MaxSupportedPrice}.";

        return null;
    }

    internal static string? ValidateCurrency(string? currency)
    {
        if (currency is not null && string.IsNullOrWhiteSpace(currency))
            return "Currency cannot be blank.";
        if (currency?.Trim().Length > MaxCurrencyLength)
            return $"Currency cannot exceed {MaxCurrencyLength} characters.";

        return null;
    }

    internal static string? ValidateDeliveryDays(int? minDeliveryDays)
    {
        if (minDeliveryDays is < 0)
            return "MinDeliveryDays cannot be negative.";
        if (minDeliveryDays > MaxDeliveryDays)
            return $"MinDeliveryDays cannot exceed {MaxDeliveryDays}.";

        return null;
    }

    internal static string NormalizeCurrency(string? currency, string fallback = "VND") =>
        string.IsNullOrWhiteSpace(currency)
            ? fallback
            : currency.Trim().ToUpperInvariant();

    internal static decimal RoundPrice(decimal price) =>
        decimal.Round(price, 4, MidpointRounding.AwayFromZero);
}
