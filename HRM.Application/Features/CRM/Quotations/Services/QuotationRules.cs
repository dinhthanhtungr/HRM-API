using HRM.Domain.Entities.CustomerSchema;

namespace HRM.Application.Features.CRM.Quotations.Services;

internal static class QuotationRules
{
    public const int MaximumCurrencyLength = 10;
    public const int MaximumUnitLength = 30;
    public const int MaximumLineCount = 500;
    public const int MaximumPriceTierCountPerLine = 100;
    public const int MaximumQuantityRangeLabelLength = 50;
    private const int MoneyScale = 6;

    public static string? TrimToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static decimal CalculateLineTotal(
        decimal quantity,
        decimal unitPrice,
        decimal discountPercent)
    {
        var grossAmount = quantity * unitPrice;
        var discountAmount = grossAmount * discountPercent / 100m;
        return Round(grossAmount - discountAmount);
    }

    public static void RecalculateTotals(Quotation quotation)
    {
        var subTotal = quotation.Lines.Sum(line => line.Quantity * line.UnitPrice);
        var discountAmount = quotation.Lines.Sum(line =>
            line.Quantity * line.UnitPrice * line.DiscountPercent / 100m);

        quotation.SubTotal = Round(subTotal);
        quotation.DiscountAmount = Round(discountAmount);
        RecalculateHeaderTax(quotation, subTotal, discountAmount);
    }

    public static void RecalculateHeaderTax(Quotation quotation)
        => RecalculateHeaderTax(quotation, quotation.SubTotal, quotation.DiscountAmount);

    private static void RecalculateHeaderTax(
        Quotation quotation,
        decimal subTotal,
        decimal discountAmount)
    {
        var taxableAmount = Math.Max(0m, subTotal - discountAmount);
        var taxPercent = Math.Clamp(quotation.TaxPercent, 0m, 100m);
        var taxAmount = taxableAmount * taxPercent / 100m;

        quotation.TaxPercent = taxPercent;
        quotation.TaxAmount = Round(taxAmount);
        quotation.TotalAmount = Round(taxableAmount + taxAmount);
    }

    public static bool IsValidPercent(decimal value) => value is >= 0m and <= 100m;

    private static decimal Round(decimal value)
        => Math.Round(value, MoneyScale, MidpointRounding.AwayFromZero);
}
