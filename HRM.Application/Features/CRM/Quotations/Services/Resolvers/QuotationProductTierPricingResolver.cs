using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Features.CRM.Quotations.Services;

internal sealed class QuotationProductTierPricingResolver
{
    private readonly ApprovedProductPricingTierReader _approvedPricingReader;
    private readonly SystemCalculatedProductPricingTierResolver _systemPricingResolver;
    private readonly LatestQuotationPricingTierReader _latestQuotationPricingReader;

    public QuotationProductTierPricingResolver(
        ApprovedProductPricingTierReader approvedPricingReader,
        SystemCalculatedProductPricingTierResolver systemPricingResolver,
        LatestQuotationPricingTierReader latestQuotationPricingReader)
    {
        _approvedPricingReader = approvedPricingReader;
        _systemPricingResolver = systemPricingResolver;
        _latestQuotationPricingReader = latestQuotationPricingReader;
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

                QuotationDefaultPriceTierSource? defaultSource = approved is { PriceTiers.Count: > 0 }
                    ? QuotationDefaultPriceTierSource.ApprovedPricingVersion
                    : system is { PriceTiers.Count: > 0 }
                        ? QuotationDefaultPriceTierSource.SystemCalculated
                        : null;

                return new ResolvedProductTierPricingReferences(
                    approved,
                    system,
                    latest,
                    defaultSource);
            });
    }
}
