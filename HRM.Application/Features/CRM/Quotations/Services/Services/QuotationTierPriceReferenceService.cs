using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Domain.Entities.CustomerSchema;

namespace HRM.Application.Features.CRM.Quotations.Services;

internal sealed class QuotationTierPriceReferenceService
{
    private const string PricingReferenceCurrency = "VND";

    private readonly QuotationProductTierPricingResolver _pricingResolver;

    public QuotationTierPriceReferenceService(
        QuotationProductTierPricingResolver pricingResolver)
    {
        _pricingResolver = pricingResolver;
    }

    public async Task EnrichAsync(
        QuotationDetailDto detail,
        Guid companyId,
        IQueryable<Quotation> visibleQuotations,
        CancellationToken cancellationToken)
    {
        var productIds = detail.Lines
            .Select(x => x.ProductId)
            .Distinct()
            .ToArray();
        if (productIds.Length == 0)
        {
            return;
        }

        var referencesByProduct = await _pricingResolver.ResolveAsync(
            productIds,
            companyId,
            PricingReferenceCurrency,
            detail.CustomerId,
            detail.QuotationId,
            visibleQuotations,
            cancellationToken);

        foreach (var line in detail.Lines)
        {
            if (!referencesByProduct.TryGetValue(line.ProductId, out var references))
            {
                continue;
            }

            var defaultTiers = references.DefaultPriceTiers;
            line.DefaultPriceTierSource = references.DefaultPriceTierSource;
            line.ApprovedPricing = QuotationProductPricingPreviewMapper.MapApproved(
                references.ApprovedPricing);
            line.SystemCalculatedPricing = QuotationProductPricingPreviewMapper.MapSystem(
                references.SystemCalculatedPricing);
            line.LatestQuotedPricing = QuotationProductPricingPreviewMapper.MapLatest(
                references.LatestQuotedPricing);

            if (line.PriceTiers.Count == 0)
            {
                // Do not persist these tiers. They are merely an editable starting point for Sale.
                line.PriceTiers = defaultTiers
                    .OrderBy(x => x.SortOrder)
                    .Select(standard => new QuotationLinePriceTierDto
                    {
                        QuotationLinePriceTierId = Guid.Empty,
                        IsSnapshot = false,
                        IsActive = true,
                        RequiresManualPrice = !standard.UnitPrice.HasValue,
                        QuantityRangeLabel = standard.QuantityRangeLabel,
                        MinQuantity = standard.MinQuantity,
                        MaxQuantity = standard.MaxQuantity,
                        MinInclusive = standard.MinInclusive,
                        MaxInclusive = standard.MaxInclusive,
                        UnitPrice = standard.UnitPrice ?? 0m,
                        CommissionAmount = 0m,
                        CustomerUnitPrice = standard.UnitPrice ?? 0m,
                        SortOrder = standard.SortOrder
                    })
                    .ToArray();
            }
        }
    }

}

internal sealed record QuotationTierPriceReference(
    string QuantityRangeLabel,
    decimal? MinQuantity,
    decimal? MaxQuantity,
    bool MinInclusive,
    bool MaxInclusive,
    decimal? UnitPrice,
    int SortOrder,
    DateTime? PriceDate);

internal static class QuotationTierPriceMatcher
{
    public static QuotationTierPriceReference? Find(
        IReadOnlyList<QuotationTierPriceReference> references,
        QuotationLinePriceTierDto target)
    {
        var exactRange = references.FirstOrDefault(x =>
            x.MinQuantity == target.MinQuantity &&
            x.MaxQuantity == target.MaxQuantity &&
            x.MinInclusive == target.MinInclusive &&
            x.MaxInclusive == target.MaxInclusive);
        if (exactRange is not null)
        {
            return exactRange;
        }

        var matchingLabel = references.FirstOrDefault(x =>
            string.Equals(
                x.QuantityRangeLabel.Trim(),
                target.QuantityRangeLabel.Trim(),
                StringComparison.OrdinalIgnoreCase));

        return matchingLabel ?? references.FirstOrDefault(x => x.SortOrder == target.SortOrder);
    }
}
