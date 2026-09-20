using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Application.Features.Pricing.Authorization;

namespace HRM.Application.Features.CRM.Quotations.Services;

internal sealed class QuotationProductTierPricingResolver
{
    private readonly ApprovedProductPricingTierReader _approvedPricingReader;
    private readonly SystemCalculatedProductPricingTierResolver _systemPricingResolver;
    private readonly LatestQuotationPricingTierReader _latestQuotationPricingReader;
    private readonly StandardPriceRealtimeComparisonQueryService _comparisonQueryService;
    private readonly IPricingVisibilityService _pricingVisibilityService;

    public QuotationProductTierPricingResolver(
        ApprovedProductPricingTierReader approvedPricingReader,
        SystemCalculatedProductPricingTierResolver systemPricingResolver,
        LatestQuotationPricingTierReader latestQuotationPricingReader,
        StandardPriceRealtimeComparisonQueryService comparisonQueryService,
        IPricingVisibilityService pricingVisibilityService)
    {
        _approvedPricingReader = approvedPricingReader;
        _systemPricingResolver = systemPricingResolver;
        _latestQuotationPricingReader = latestQuotationPricingReader;
        _comparisonQueryService = comparisonQueryService;
        _pricingVisibilityService = pricingVisibilityService;
    }

    public async Task<IReadOnlyDictionary<Guid, ResolvedProductTierPricingReferences>> ResolveAsync(
        IReadOnlyCollection<Guid> productIds,
        Guid companyId,
        string currency,
        Guid? customerId,
        Guid? excludedQuotationId,
        IQueryable<Quotation> visibleQuotations,
        CancellationToken cancellationToken)
    {
        var normalizedProductIds = productIds
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToArray();
        if (normalizedProductIds.Length == 0)
        {
            return new Dictionary<Guid, ResolvedProductTierPricingReferences>();
        }

        // These readers share the scoped EF context, so execute sequentially.
        var approvedByProduct = await _approvedPricingReader.LoadAsync(
            normalizedProductIds,
            companyId,
            currency,
            cancellationToken);
        var systemByProduct = await _systemPricingResolver.ResolveAsync(
            normalizedProductIds,
            companyId,
            currency,
            cancellationToken);
        var comparisonsByProduct = await _comparisonQueryService.LoadVisibleAsync(
            approvedByProduct.Values
                .Select(x => new StandardPriceRealtimeComparisonRequest(
                    x.ProductId,
                    x.Currency,
                    x.StandardSellingPrice,
                    x.MaterialCostSnapshot,
                    x.SourceType,
                    x.SourceId))
                .ToArray(),
            companyId,
            _pricingVisibilityService.GetAccess(),
            cancellationToken);
        var latestByProduct = customerId.HasValue
            ? await _latestQuotationPricingReader.LoadAsync(
                normalizedProductIds,
                customerId.Value,
                currency,
                excludedQuotationId,
                visibleQuotations,
                cancellationToken)
            : new Dictionary<Guid, LatestQuotedTierPricingReference>();

        return normalizedProductIds.ToDictionary(
            productId => productId,
            productId =>
            {
                approvedByProduct.TryGetValue(productId, out var approved);
                systemByProduct.TryGetValue(productId, out var system);
                latestByProduct.TryGetValue(productId, out var latest);
                comparisonsByProduct.TryGetValue(productId, out var comparison);

                QuotationDefaultPriceTierSource? defaultSource = approved is { PriceTiers.Count: > 0 }
                    ? QuotationDefaultPriceTierSource.ApprovedPricingVersion
                    : system is { PriceTiers.Count: > 0 }
                        ? QuotationDefaultPriceTierSource.SystemCalculated
                        : null;

                return new ResolvedProductTierPricingReferences(
                    approved,
                    system,
                    latest,
                    defaultSource,
                    comparison);
            });
    }
}
