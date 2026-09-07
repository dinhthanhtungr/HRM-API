using System.Globalization;
using System.Text;
using HRM.Domain.Entities.CustomerSchema;

namespace HRM.Application.Features.CRM.Quotations.Services;

internal static class QuotationSentContentBuilder
{
    private static readonly CultureInfo NumberCulture = CultureInfo.InvariantCulture;

    public static string Build(Quotation quotation)
    {
        var sentDate = quotation.SentDate ?? throw new InvalidOperationException(
            "SentDate is required to build the quotation sent content.");
        var currency = QuotationRules.TrimToNull(quotation.Currency)?.ToUpperInvariant()
            ?? throw new InvalidOperationException(
                "Quotation currency snapshot is required to build sent content.");
        var content = new StringBuilder();

        content.AppendLine($"Báo giá: {quotation.ExternalId}");
        content.AppendLine($"Ngày gửi: {sentDate:dd/MM/yyyy}");
        content.AppendLine($"Tiền tệ: {currency}");
        content.AppendLine();
        content.AppendLine("Sản phẩm:");

        foreach (var line in quotation.Lines.Where(x => x.IsActive).OrderBy(x => x.SortOrder).ThenBy(x => x.QuotationLineId))
        {
            content.AppendLine($"- [{line.ProductExternalIdSnapshot}] {line.ProductNameSnapshot}");

            content.AppendLine("  Giá theo khối lượng:");
            foreach (var tier in line.PriceTiers.Where(x => x.IsActive)
                         .OrderBy(x => x.SortOrder)
                         .ThenBy(x => x.QuotationLinePriceTierId))
            {
                content.AppendLine(
                    $"  - {ResolveTierLabel(tier)}: " +
                    $"{FormatPrice(tier.CustomerUnitPrice, currency, line.Unit)}");
            }

            content.AppendLine($"  Ghi chú: {QuotationRules.TrimToNull(line.Note) ?? "-"}");
        }

        var quotationNote = QuotationRules.TrimToNull(quotation.Note);
        if (quotationNote is not null)
        {
            content.AppendLine();
            content.AppendLine($"Ghi chú báo giá: {quotationNote}");
        }

        return content.ToString().TrimEnd();
    }

    private static string FormatPrice(decimal price, string currency, string unit)
    {
        var normalizedUnit = QuotationRules.TrimToNull(unit);
        var unitSuffix = normalizedUnit is null ? string.Empty : $"/{normalizedUnit}";
        return $"{price.ToString("#,##0.##", NumberCulture)} {currency}{unitSuffix}";
    }

    private static string ResolveTierLabel(QuotationLinePriceTier tier)
    {
        var label = QuotationRules.TrimToNull(tier.QuantityRangeLabel);
        if (label is not null)
        {
            return label;
        }

        if (!tier.MinQuantity.HasValue && tier.MaxQuantity.HasValue)
        {
            return $"{(tier.MaxInclusive ? "<=" : "<")} {FormatQuantity(tier.MaxQuantity.Value)}";
        }

        if (tier.MinQuantity.HasValue && !tier.MaxQuantity.HasValue)
        {
            return $"{(tier.MinInclusive ? ">=" : ">")} {FormatQuantity(tier.MinQuantity.Value)}";
        }

        if (tier.MinQuantity.HasValue && tier.MaxQuantity.HasValue)
        {
            return $"{FormatQuantity(tier.MinQuantity.Value)}-{FormatQuantity(tier.MaxQuantity.Value)}";
        }

        return "Tất cả số lượng";
    }

    private static string FormatQuantity(decimal quantity)
        => quantity.ToString("#,##0.##", NumberCulture);
}
