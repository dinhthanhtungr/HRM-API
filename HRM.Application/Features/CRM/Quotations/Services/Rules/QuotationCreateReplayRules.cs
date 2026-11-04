using System.Security.Cryptography;
using System.Text.Json;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Domain.Entities.CustomerSchema;

namespace HRM.Application.Features.CRM.Quotations.Services;

internal static class QuotationCreateReplayRules
{
    internal static Guid Id(Guid companyId, Guid key)
    {
        var hash = SHA256.HashData(companyId.ToByteArray().Concat(key.ToByteArray()).ToArray());
        return new Guid(hash.AsSpan(0, 16));
    }

    internal static bool Matches(Quotation saved, CreateQuotationRequest request)
    {
        if (saved.CustomerId != request.CustomerId || saved.ContactId != request.ContactId ||
            saved.Currency != request.Currency.Trim().ToUpperInvariant() || saved.ExchangeRate != request.ExchangeRate ||
            saved.TaxPercent != request.TaxPercent || !SameDate(saved.ValidUntil, request.ValidUntil) ||
            (request.QuotationDate.HasValue && !SameDate(saved.QuotationDate, request.QuotationDate)) ||
            saved.Note != QuotationRules.TrimToNull(request.Note) ||
            saved.PaymentTerms != QuotationRules.TrimToNull(request.PaymentTerms) ||
            saved.DeliveryTerms != QuotationRules.TrimToNull(request.DeliveryTerms) ||
            (!string.IsNullOrWhiteSpace(request.ExternalId) && saved.ExternalId != request.ExternalId.Trim()) ||
            (request.ContactName is not null && saved.ContactName != QuotationRules.TrimToNull(request.ContactName)) ||
            (request.ContactPhone is not null && saved.ContactPhone != QuotationRules.TrimToNull(request.ContactPhone)) ||
            (request.CustomerAddressSnapshot is not null && saved.CustomerAddressSnapshot != QuotationRules.TrimToNull(request.CustomerAddressSnapshot))) return false;
        var lines = saved.Lines.OrderBy(x => x.SortOrder).ToArray();
        var incoming = request.Lines.OrderBy(x => x.SortOrder).ToArray();
        if (lines.Length != incoming.Length) return false;
        for (var i = 0; i < lines.Length; i++)
        {
            var a = lines[i]; var b = incoming[i];
            if (a.ProductId != b.ProductId || a.SampleRequestId != b.SampleRequestId || a.ProductPricingVersionId != b.ProductPricingVersionId ||
                a.PriceMode != b.PriceMode || a.Quantity != b.Quantity || a.Unit != b.Unit?.Trim() ||
                a.SortOrder != (b.SortOrder ?? i) || a.DiscountPercent != b.DiscountPercent || a.IsActive != b.IsActive || a.Note != QuotationRules.TrimToNull(b.Note) ||
                (!b.ProductPricingVersionId.HasValue && a.UnitPrice != b.UnitPrice)) return false;
            var oldTiers = a.PriceTiers.OrderBy(x => x.SortOrder).Select(x => (x.QuantityRangeLabel, x.MinQuantity, x.MaxQuantity, x.MinInclusive, x.MaxInclusive, x.UnitPrice, x.CommissionAmount, x.SortOrder, x.IsActive));
            var newTiers = b.PriceTiers.OrderBy(x => x.SortOrder).Select(x => (x.QuantityRangeLabel.Trim(), x.MinQuantity, x.MaxQuantity, x.MinInclusive, x.MaxInclusive, x.UnitPrice, x.CommissionAmount, x.SortOrder, x.IsActive));
            if (!oldTiers.SequenceEqual(newTiers)) return false;
        }
        var oldTerms = saved.Terms.OrderBy(x => x.SortOrder).Select(x => new { x.LabelVi, x.LabelEn, x.ValueVi, x.ValueEn, x.SortOrder, x.IsActive });
        var newTerms = request.Terms.OrderBy(x => x.SortOrder).Select(x => new { LabelVi = x.LabelVi.Trim(), LabelEn = QuotationRules.TrimToNull(x.LabelEn), ValueVi = QuotationRules.TrimToNull(x.ValueVi), ValueEn = QuotationRules.TrimToNull(x.ValueEn), x.SortOrder, x.IsActive });
        return JsonSerializer.Serialize(oldTerms) == JsonSerializer.Serialize(newTerms);
    }

    private static bool SameDate(DateTime? left, DateTime? right)
        => left.HasValue == right.HasValue && (!left.HasValue || left.Value.Ticks / 10 == right!.Value.Ticks / 10);
}
