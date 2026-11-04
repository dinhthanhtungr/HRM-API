using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Domain.Entities.CustomerSchema;

namespace HRM.Application.Features.CRM.Quotations.Services;

/// <summary>Approval binds customer commercial terms, not the current standard-price reference.</summary>
internal static class QuotationConfirmationCommercialRules
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new CanonicalDecimalConverter() }
    };

    public static string Fingerprint(Quotation quotation)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            quotation.CustomerId, quotation.ContactId, quotation.ContactName, quotation.ContactPhone,
            quotation.CustomerAddressSnapshot, quotation.Currency,
            ExchangeRate = Round(quotation.ExchangeRate, 6),
            DiscountAmount = Round(quotation.DiscountAmount, 6), TaxPercent = Round(quotation.TaxPercent, 4),
            QuotationDate = quotation.QuotationDate.Ticks / 10, ValidUntil = quotation.ValidUntil?.Ticks / 10,
            quotation.PaymentTerms, quotation.DeliveryTerms, quotation.Note,
            Lines = quotation.Lines.Where(x => x.IsActive).OrderBy(x => x.QuotationLineId).Select(x => new
            {
                x.QuotationLineId, x.ProductId, x.ProductExternalIdSnapshot, x.ProductNameSnapshot,
                Quantity = Round(x.Quantity, 6), x.Unit, UnitPrice = Round(x.UnitPrice, 6),
                DiscountPercent = Round(x.DiscountPercent, 4), x.Note,
                Tiers = x.PriceTiers.Where(t => t.IsActive).OrderBy(t => t.QuotationLinePriceTierId).Select(t => new
                {
                    t.QuotationLinePriceTierId, t.IsActive,
                    MinQuantity = t.MinQuantity.HasValue ? Round(t.MinQuantity.Value, 3) : (decimal?)null,
                    MaxQuantity = t.MaxQuantity.HasValue ? Round(t.MaxQuantity.Value, 3) : (decimal?)null,
                    t.MinInclusive, t.MaxInclusive, UnitPrice = Round(t.UnitPrice, 6),
                    CommissionAmount = Round(t.CommissionAmount, 6), t.QuantityRangeLabel
                })
            }),
            Terms = quotation.Terms.Where(x => x.IsActive).OrderBy(x => x.QuotationTermId).Select(x => new
            {
                x.QuotationTermId, x.LabelVi, x.LabelEn, x.ValueVi, x.ValueEn
            })
        }, Options);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private static decimal Round(decimal value, int scale)
        => decimal.Round(value, scale, MidpointRounding.AwayFromZero);

    public static string? BlockedReason(Quotation quotation, QuotationSendConfirmationPayload payload)
    {
        if (payload.ExpectedUpdatedDate != quotation.UpdatedDate)
            return QuotationConfirmationErrors.RequestOutdated;
        if (payload.Comparison is null || payload.Comparison.Count == 0 || payload.Comparison.Any(x => x is null)
            || (payload.CommercialFingerprintVersion.HasValue && payload.CommercialFingerprint is null))
            return QuotationConfirmationErrors.SnapshotOutdated;
        // Legacy messages retain their original snapshot and use the existing revision token.
        if (payload.CommercialFingerprint is not null &&
            (payload.CommercialFingerprintVersion != 1 || payload.CommercialFingerprint != Fingerprint(quotation)))
            return QuotationConfirmationErrors.CommercialChanged;
        return null;
    }

    private sealed class CanonicalDecimalConverter : JsonConverter<decimal>
    {
        public override decimal Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
            => reader.GetDecimal();
        public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
            => writer.WriteRawValue(value.ToString("G29", CultureInfo.InvariantCulture));
    }
}
