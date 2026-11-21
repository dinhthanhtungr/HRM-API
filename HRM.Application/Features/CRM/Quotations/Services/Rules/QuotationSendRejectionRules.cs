using System.Security.Cryptography;
using System.Text.Json;
using System.Globalization;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Features.CRM.Quotations.Services;

internal static class QuotationSendRejectionRules
{
    public const string PdfBlockedCode = "quotation.send.pdf_blocked_by_rejection";
    public const string PricesUnchangedCode = "quotation.send.rejected_prices_unchanged";
    public static bool CanExportPdf(QuotationStatus status, DateTime? sentAt, DateTime? rejectedAt)
        => status == QuotationStatus.Sent || (status != QuotationStatus.SendRejected && (!rejectedAt.HasValue || sentAt >= rejectedAt));

    public static string PricingFingerprint(Quotation quotation)
        => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
            quotation.Lines.Where(x => x.IsActive).Select(x => JsonSerializer.Serialize(new
            {
                x.ProductId,
                Tiers = x.PriceTiers.Where(t => t.IsActive).OrderBy(t => t.MinQuantity).ThenBy(t => t.MaxQuantity)
                    .ThenBy(t => t.MinInclusive).ThenBy(t => t.MaxInclusive).ThenBy(t => t.UnitPrice).ThenBy(t => t.CommissionAmount).Select(t => new
                {
                    MinQuantity = t.MinQuantity.HasValue ? Number(t.MinQuantity.Value) : null,
                    MaxQuantity = t.MaxQuantity.HasValue ? Number(t.MaxQuantity.Value) : null,
                    MinInclusive = !t.MinQuantity.HasValue || t.MinInclusive,
                    MaxInclusive = !t.MaxQuantity.HasValue || t.MaxInclusive,
                    UnitPrice = Number(t.UnitPrice), CommissionAmount = Number(t.CommissionAmount)
                })
            })).OrderBy(x => x, StringComparer.Ordinal))));

    private static string Number(decimal value) => value.ToString("G29", CultureInfo.InvariantCulture);
}
