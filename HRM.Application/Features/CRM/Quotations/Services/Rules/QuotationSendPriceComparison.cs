using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Domain.Entities.CustomerSchema;

namespace HRM.Application.Features.CRM.Quotations.Services;

/// <summary>Match the FE dominant positive-overlap rule; without positive overlap use boundary, adjacent-gap and outside-table references.</summary>
internal static class QuotationSendPriceComparison
{
    public static IReadOnlyList<QuotationSendPriceSegment> Compare(
        QuotationLine line, ProductPricingVersion standard,
        string quotationCurrency = "VND", decimal exchangeRate = 1m)
    {
        if (!QuotationPricingCurrencyConverter.IsStandardPricingCurrency(standard.Currency))
            throw new InvalidOperationException("Approved standard pricing must be VND.");
        if (!QuotationPricingCurrencyConverter.TryValidateQuotationCurrency(quotationCurrency, exchangeRate, out var error))
            throw new InvalidOperationException(error);
        var standards = standard.PriceTiers.Where(x => x.IsActive).ToArray();
        if (standards.Length == 0)
            throw new InvalidOperationException($"Approved standard has no active price tiers for {line.ProductExternalIdSnapshot}.");
        if (standards.Any(x => x.UnitPrice < 0m || InvalidRange(x.MinQuantity, x.MaxQuantity, x.MinInclusive, x.MaxInclusive)))
            throw new InvalidOperationException($"Approved standard contains an invalid price or quantity range for {line.ProductExternalIdSnapshot}.");

        var highestPrice = standards.Max(x => x.UnitPrice);
        var result = new List<QuotationSendPriceSegment>();
        foreach (var customer in line.PriceTiers.Where(x => x.IsActive))
        {
            if (InvalidRange(customer.MinQuantity, customer.MaxQuantity, customer.MinInclusive, customer.MaxInclusive))
                throw new InvalidOperationException($"Customer quantity range is invalid for {line.ProductExternalIdSnapshot}.");
            // Match the customer-facing preview: longest positive overlap wins, ties use the higher price.
            // Unbounded intersections have infinite length, matching FE null-bound semantics.
            var overlaps = standards.Select(tier => new
            {
                Tier = tier,
                Lower = Math.Max(customer.MinQuantity ?? 0m, tier.MinQuantity ?? 0m),
                Upper = customer.MaxQuantity.HasValue && tier.MaxQuantity.HasValue
                    ? Math.Min(customer.MaxQuantity.Value, tier.MaxQuantity.Value)
                    : customer.MaxQuantity ?? tier.MaxQuantity
            }).Where(x => (!customer.MinQuantity.HasValue && !x.Tier.MinQuantity.HasValue) ||
                !x.Upper.HasValue || x.Upper.Value > x.Lower)
                .Select(x => new { x.Tier, Length = x.Upper.HasValue && (customer.MinQuantity.HasValue || x.Tier.MinQuantity.HasValue)
                    ? x.Upper.Value - x.Lower : (decimal?)null })
                .ToArray();
            var dominant = overlaps.OrderBy(x => x.Length.HasValue)
                .ThenByDescending(x => x.Length).ThenByDescending(x => x.Tier.UnitPrice)
                .FirstOrDefault()?.Tier;
            var cuts = standards.SelectMany(x => new[] { x.MinQuantity, x.MaxQuantity })
                .Concat([customer.MinQuantity, customer.MaxQuantity, 0m])
                .Where(x => x.HasValue).Select(x => x!.Value).Distinct().Order().ToArray();
            var segments = new List<QuotationSendPriceSegment>();
            // Partition into exact boundary points and open intervals; no midpoint rounding or quantity-step assumption.
            for (var i = 0; i < cuts.Length; i++)
            {
                var min = cuts[i];
                if (Contains(customer.MinQuantity, customer.MaxQuantity, customer.MinInclusive, customer.MaxInclusive, min))
                    AddSegment(min, min, true, true);
                decimal? max = i + 1 < cuts.Length ? cuts[i + 1] : null;
                if (min >= (customer.MinQuantity ?? 0m)
                    && (!customer.MaxQuantity.HasValue || (max.HasValue && max.Value <= customer.MaxQuantity.Value)))
                    AddSegment(min, max, false, false);
            }
            var referencePrice = segments.Max(x => x.StandardUnitPrice);
            result.AddRange(segments.Select(x => x with { ReferenceUnitPrice = referencePrice }));

            void AddSegment(decimal min, decimal? max, bool minInclusive, bool maxInclusive)
            {
                var covering = standards.Where(x => max == min
                    ? Contains(x.MinQuantity, x.MaxQuantity, x.MinInclusive, x.MaxInclusive, min)
                    : (x.MinQuantity ?? 0m) <= min
                        && (!x.MaxQuantity.HasValue || (max.HasValue && x.MaxQuantity.Value >= max.Value)))
                    .ToArray();
                if (covering.Length > 1)
                    throw new InvalidOperationException($"Approved standard has overlapping quantity ranges for {line.ProductExternalIdSnapshot}.");
                var (price, basis) = dominant is not null
                    ? (dominant.UnitPrice, QuotationSendPriceSegment.ApprovedTier)
                    : covering.Length == 1
                    ? (covering[0].UnitPrice, QuotationSendPriceSegment.ApprovedTier)
                    : ResolveFallback(standards, min, max, highestPrice);
                var segment = new QuotationSendPriceSegment(line.QuotationLineId, line.ProductExternalIdSnapshot,
                    standard.ProductPricingVersionId, standard.ApprovedAt ?? standard.UpdatedDate ?? standard.CreatedDate,
                    min, max, minInclusive, maxInclusive, RoundPrice(
                        QuotationPricingCurrencyConverter.ConvertFromStandardPricing(price, quotationCurrency, exchangeRate)),
                    RoundPrice(customer.UnitPrice) + RoundPrice(customer.CommissionAmount))
                {
                    ComparisonBasis = basis, CustomerPriceTierId = customer.QuotationLinePriceTierId,
                    CustomerMinQuantity = customer.MinQuantity, CustomerMaxQuantity = customer.MaxQuantity,
                    CustomerMinInclusive = customer.MinInclusive, CustomerMaxInclusive = customer.MaxInclusive,
                    Currency = quotationCurrency, StandardVersion = standard.Version
                };
                if (segments.Count > 0 && segments[^1] is var previous
                    && previous.StandardUnitPrice == segment.StandardUnitPrice && previous.ComparisonBasis == basis
                    && previous.MaxQuantity == min && (previous.MaxInclusive || minInclusive))
                    segments[^1] = previous with { MaxQuantity = max, MaxInclusive = maxInclusive };
                else
                    segments.Add(segment);
            }
        }
        return result;
    }

    // Match the existing numeric(22,6) persistence precision on both sides, including staged saves.
    private static decimal RoundPrice(decimal value) => decimal.Round(value, 6, MidpointRounding.AwayFromZero);

    private static (decimal Price, string Basis) ResolveFallback(
        ProductPricingTier[] standards, decimal min, decimal? max, decimal highestPrice)
    {
        var leftEnd = standards.Where(x => x.MaxQuantity.HasValue && x.MaxQuantity.Value <= min).Max(x => x.MaxQuantity);
        var rightStart = max.HasValue
            ? standards.Where(x => x.MinQuantity.HasValue && x.MinQuantity.Value >= max.Value).Min(x => x.MinQuantity)
            : null;
        if (leftEnd.HasValue && rightStart.HasValue)
        {
            var leftPrice = standards.Where(x => x.MaxQuantity == leftEnd).Max(x => x.UnitPrice);
            var rightPrice = standards.Where(x => x.MinQuantity == rightStart).Max(x => x.UnitPrice);
            return (Math.Max(leftPrice, rightPrice), QuotationSendPriceSegment.AdjacentTierFallback);
        }
        if (leftEnd.HasValue && !rightStart.HasValue)
        {
            var lastPrice = standards.Where(x => x.MaxQuantity == leftEnd).Max(x => x.UnitPrice);
            return (lastPrice, QuotationSendPriceSegment.LastApprovedTierFallback);
        }
        return (highestPrice, QuotationSendPriceSegment.HighestApprovedTierFallback);
    }

    private static bool InvalidRange(decimal? min, decimal? max, bool minInclusive, bool maxInclusive)
        => min < 0m || max < 0m || max < (min ?? 0m)
            || (max == (min ?? 0m) && (!maxInclusive || (min.HasValue && !minInclusive)));

    private static bool Contains(decimal? min, decimal? max, bool minInclusive, bool maxInclusive, decimal value)
        => value >= 0m && (!min.HasValue || (minInclusive ? value >= min : value > min))
            && (!max.HasValue || (maxInclusive ? value <= max : value < max));
}
