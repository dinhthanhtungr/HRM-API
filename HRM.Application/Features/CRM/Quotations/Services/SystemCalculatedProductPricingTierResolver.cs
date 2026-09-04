using HRM.Application.Abstractions.Commons.Time;

namespace HRM.Application.Features.CRM.Quotations.Services;

internal sealed class SystemCalculatedProductPricingTierResolver
{
    private readonly ProductPricingRealtimeSourceQueryService _sourceQueryService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public SystemCalculatedProductPricingTierResolver(
        ProductPricingRealtimeSourceQueryService sourceQueryService,
        IDateTimeProvider dateTimeProvider)
    {
        _sourceQueryService = sourceQueryService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<IReadOnlyDictionary<Guid, SystemCalculatedTierPricingReference>> ResolveAsync(
        IReadOnlyCollection<Guid> productIds,
        Guid companyId,
        string currency,
        CancellationToken cancellationToken)
    {
        if (productIds.Count == 0)
        {
            return new Dictionary<Guid, SystemCalculatedTierPricingReference>();
        }

        var sources = await _sourceQueryService.LoadAsync(
            productIds,
            companyId,
            currency,
            cancellationToken);
        var calculatedAt = _dateTimeProvider.Now;
        var result = new Dictionary<Guid, SystemCalculatedTierPricingReference>();

        foreach (var productId in productIds)
        {
            var source = ProductPricingWorkbenchSourceSelector.ChooseFallback(
                sources.GetValueOrDefault(productId) ?? []);
            if (source is null)
            {
                continue;
            }

            var tiers = (source.Pricing?.SuggestedPriceTiers ?? source.PriceTierTemplates)
                .OrderBy(x => x.SortOrder)
                .Select(x => new QuotationTierPriceReference(
                    x.QuantityRangeLabel,
                    x.MinQuantity,
                    x.MaxQuantity,
                    x.MinInclusive,
                    x.MaxInclusive,
                    x.UnitPrice,
                    x.SortOrder,
                    null))
                .ToArray();

            result[productId] = new SystemCalculatedTierPricingReference(
                productId,
                source.PricingStatus,
                calculatedAt,
                source.StandardSellingPrice,
                source.SourceType,
                source.SourceId,
                source.ExternalId,
                source.Name,
                tiers);
        }

        return result;
    }
}
