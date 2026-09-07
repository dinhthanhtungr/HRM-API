namespace HRM.Application.Features.CRM.Quotations.Services;

/// <summary>
/// Chuyển giá chuẩn VND sang tiền tệ snapshot của báo giá.
/// ExchangeRate có nghĩa là số VND tương ứng một đơn vị tiền tệ của báo giá.
/// </summary>
internal static class QuotationPricingCurrencyConverter
{
    public static bool TryValidateQuotationCurrency(
        string quotationCurrency,
        decimal exchangeRate,
        out string? error)
    {
        if (string.IsNullOrWhiteSpace(quotationCurrency))
        {
            error = "Quotation currency is required to convert standard pricing.";
            return false;
        }

        if (!IsStandardPricingCurrency(quotationCurrency) && exchangeRate <= 0m)
        {
            error = "ExchangeRate must be greater than zero to convert VND standard pricing.";
            return false;
        }

        error = null;
        return true;
    }

    public static decimal ConvertFromStandardPricing(
        decimal value,
        string quotationCurrency,
        decimal exchangeRate)
        => IsStandardPricingCurrency(quotationCurrency)
            ? value
            : value / exchangeRate;

    public static bool IsStandardPricingCurrency(string currency)
        => string.Equals(
            currency,
            ProductPricingSourceRules.StandardPricingCurrency,
            StringComparison.OrdinalIgnoreCase);
}
