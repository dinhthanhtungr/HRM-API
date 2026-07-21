using HRM.Domain.Entities.CustomerSchema;

namespace HRM.Application.Features.CRM.Quotations.Services;

internal static class QuotationRules
{
    public const int MaximumCurrencyLength = 10;
    public const int MaximumUnitLength = 30;
    public const int MaximumLineCount = 500;
    private const int MoneyScale = 6;

    public static string? TrimToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static decimal CalculateLineTotal(
        decimal quantity,
        decimal unitPrice,
        decimal discountPercent,
        decimal taxPercent)
    {
        var grossAmount = quantity * unitPrice;
        var discountAmount = grossAmount * discountPercent / 100m;
        var taxableAmount = grossAmount - discountAmount;
        var taxAmount = taxableAmount * taxPercent / 100m;
        return Round(taxableAmount + taxAmount);
    }

    public static void RecalculateTotals(Quotation quotation)
    {
        var subTotal = quotation.Lines.Sum(line => line.Quantity * line.UnitPrice);
        var discountAmount = quotation.Lines.Sum(line =>
            line.Quantity * line.UnitPrice * line.DiscountPercent / 100m);
        var taxAmount = quotation.Lines.Sum(line =>
        {
            var grossAmount = line.Quantity * line.UnitPrice;
            var lineDiscount = grossAmount * line.DiscountPercent / 100m;
            return (grossAmount - lineDiscount) * line.TaxPercent / 100m;
        });

        quotation.SubTotal = Round(subTotal);
        quotation.DiscountAmount = Round(discountAmount);
        quotation.TaxAmount = Round(taxAmount);
        quotation.TotalAmount = Round(subTotal - discountAmount + taxAmount);
    }

    public static bool IsValidPercent(decimal value) => value is >= 0m and <= 100m;

    private static decimal Round(decimal value)
        => Math.Round(value, MoneyScale, MidpointRounding.AwayFromZero);
}
