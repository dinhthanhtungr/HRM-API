using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.CustomerEnum;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Services;

internal sealed class QuotationTierPriceReferenceService
{
    private readonly ICRMReadDbContext _dbContext;
    private readonly ProductPricingRealtimeSourceQueryService _sourceQueryService;

    public QuotationTierPriceReferenceService(
        ICRMReadDbContext dbContext,
        ProductPricingRealtimeSourceQueryService sourceQueryService)
    {
        _dbContext = dbContext;
        _sourceQueryService = sourceQueryService;
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

        var standardPrices = await LoadStandardPricesAsync(
            companyId,
            detail.Currency,
            productIds,
            cancellationToken);
        var latestQuotedPrices = await LoadLatestQuotedPricesAsync(
            detail.QuotationId,
            detail.Currency,
            productIds,
            visibleQuotations,
            cancellationToken);

        foreach (var line in detail.Lines)
        {
            standardPrices.TryGetValue(line.ProductId, out var standardTiers);
            latestQuotedPrices.TryGetValue(line.ProductId, out var latestTiers);

            foreach (var tier in line.PriceTiers)
            {
                var standard = QuotationTierPriceMatcher.Find(standardTiers ?? [], tier);
                tier.StandardUnitPrice = standard?.UnitPrice;
                tier.StandardPriceUpdatedDate = standard?.PriceDate;

                var latestQuoted = QuotationTierPriceMatcher.Find(latestTiers ?? [], tier);
                tier.LatestQuotedUnitPrice = latestQuoted?.UnitPrice;
                tier.LatestQuotedDate = latestQuoted?.PriceDate;
            }
        }
    }

    private async Task<IReadOnlyDictionary<Guid, IReadOnlyList<QuotationTierPriceReference>>>
        LoadStandardPricesAsync(
            Guid companyId,
            string currency,
            IReadOnlyCollection<Guid> productIds,
            CancellationToken cancellationToken)
    {
        var approvedVersions = await _dbContext.ProductPricingVersions
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == companyId &&
                productIds.Contains(x.ProductId) &&
                x.Currency == currency &&
                x.Status == ProductPricingStatus.Approved &&
                x.IsActive)
            .OrderByDescending(x => x.Version)
            .ThenByDescending(x => x.UpdatedDate ?? x.CreatedDate)
            .Select(x => new ApprovedPricingVersionRow
            {
                ProductPricingVersionId = x.ProductPricingVersionId,
                ProductId = x.ProductId,
                PriceDate = x.UpdatedDate ?? x.ApprovedAt ?? x.CreatedDate
            })
            .ToListAsync(cancellationToken);

        var latestVersions = approvedVersions
            .GroupBy(x => x.ProductId)
            .Select(x => x.First())
            .ToArray();
        var versionById = latestVersions.ToDictionary(
            x => x.ProductPricingVersionId,
            x => x);
        var versionIds = versionById.Keys.ToArray();

        var storedTierRows = versionIds.Length == 0
            ? []
            : await _dbContext.ProductPricingTiers
                .AsNoTracking()
                .Where(x => versionIds.Contains(x.ProductPricingVersionId))
                .Select(x => new StoredPricingTierRow
                {
                    ProductPricingVersionId = x.ProductPricingVersionId,
                    QuantityRangeLabel = x.QuantityRangeLabel,
                    MinQuantity = x.MinQuantity,
                    MaxQuantity = x.MaxQuantity,
                    MinInclusive = x.MinInclusive,
                    MaxInclusive = x.MaxInclusive,
                    UnitPrice = x.UnitPrice,
                    SortOrder = x.SortOrder
                })
                .ToListAsync(cancellationToken);

        var result = storedTierRows
            .Where(x => versionById.ContainsKey(x.ProductPricingVersionId))
            .GroupBy(x => versionById[x.ProductPricingVersionId].ProductId)
            .ToDictionary(
                x => x.Key,
                x => (IReadOnlyList<QuotationTierPriceReference>)x
                    .Select(tier =>
                    {
                        var version = versionById[tier.ProductPricingVersionId];
                        return new QuotationTierPriceReference(
                            tier.QuantityRangeLabel,
                            tier.MinQuantity,
                            tier.MaxQuantity,
                            tier.MinInclusive,
                            tier.MaxInclusive,
                            tier.UnitPrice,
                            tier.SortOrder,
                            version.PriceDate);
                    })
                    .OrderBy(tier => tier.SortOrder)
                    .ToArray());

        var productsWithoutStoredTiers = productIds
            .Where(productId => !result.ContainsKey(productId))
            .ToArray();
        if (productsWithoutStoredTiers.Length == 0)
        {
            return result;
        }

        var sources = await _sourceQueryService.LoadAsync(
            productsWithoutStoredTiers,
            companyId,
            currency,
            cancellationToken);
        foreach (var productId in productsWithoutStoredTiers)
        {
            var source = ProductPricingWorkbenchSourceSelector.ChooseFallback(
                sources.GetValueOrDefault(productId) ?? []);
            if (source is null)
            {
                continue;
            }

            var pricing = ProductPricingWorkbenchMapper.BuildEffectivePricing(null, source);
            var suggestedTiers = pricing?.SuggestedPriceTiers ?? source.PriceTierTemplates;
            result[productId] = suggestedTiers
                .Where(x => x.UnitPrice.HasValue)
                .Select(x => new QuotationTierPriceReference(
                    x.QuantityRangeLabel,
                    x.MinQuantity,
                    x.MaxQuantity,
                    x.MinInclusive,
                    x.MaxInclusive,
                    x.UnitPrice!.Value,
                    x.SortOrder,
                    null))
                .OrderBy(x => x.SortOrder)
                .ToArray();
        }

        return result;
    }

    private async Task<IReadOnlyDictionary<Guid, IReadOnlyList<QuotationTierPriceReference>>>
        LoadLatestQuotedPricesAsync(
            Guid currentQuotationId,
            string currency,
            IReadOnlyCollection<Guid> productIds,
            IQueryable<Quotation> visibleQuotations,
            CancellationToken cancellationToken)
    {
        var candidates = await visibleQuotations
            .Where(x =>
                x.QuotationId != currentQuotationId &&
                x.IsActive &&
                x.Currency == currency &&
                x.SentDate.HasValue)
            .SelectMany(quotation => quotation.Lines
                .Where(line =>
                    productIds.Contains(line.ProductId) &&
                    line.PriceTiers.Any())
                .Select(line => new LatestQuotationLineRow
                {
                    ProductId = line.ProductId,
                    QuotationLineId = line.QuotationLineId,
                    QuotationId = quotation.QuotationId,
                    SentDate = quotation.SentDate!.Value,
                    QuotationUpdatedDate = quotation.UpdatedDate,
                    LineSortOrder = line.SortOrder
                }))
            .ToListAsync(cancellationToken);

        var latestLines = candidates
            .GroupBy(x => x.ProductId)
            .Select(group => group
                .OrderByDescending(x => x.SentDate)
                .ThenByDescending(x => x.QuotationUpdatedDate)
                .ThenByDescending(x => x.QuotationId)
                .ThenBy(x => x.LineSortOrder)
                .ThenBy(x => x.QuotationLineId)
                .First())
            .ToArray();
        var lineById = latestLines.ToDictionary(x => x.QuotationLineId, x => x);
        var lineIds = lineById.Keys.ToArray();
        if (lineIds.Length == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<QuotationTierPriceReference>>();
        }

        var tierRows = await _dbContext.QuotationLinePriceTiers
            .AsNoTracking()
            .Where(x => lineIds.Contains(x.QuotationLineId))
            .Select(x => new LatestQuotationTierRow
            {
                QuotationLineId = x.QuotationLineId,
                QuantityRangeLabel = x.QuantityRangeLabel,
                MinQuantity = x.MinQuantity,
                MaxQuantity = x.MaxQuantity,
                MinInclusive = x.MinInclusive,
                MaxInclusive = x.MaxInclusive,
                UnitPrice = x.UnitPrice,
                SortOrder = x.SortOrder
            })
            .ToListAsync(cancellationToken);

        return tierRows
            .GroupBy(x => lineById[x.QuotationLineId].ProductId)
            .ToDictionary(
                x => x.Key,
                x => (IReadOnlyList<QuotationTierPriceReference>)x
                    .Select(tier => new QuotationTierPriceReference(
                        tier.QuantityRangeLabel,
                        tier.MinQuantity,
                        tier.MaxQuantity,
                        tier.MinInclusive,
                        tier.MaxInclusive,
                        tier.UnitPrice,
                        tier.SortOrder,
                        lineById[tier.QuotationLineId].SentDate))
                    .OrderBy(tier => tier.SortOrder)
                    .ToArray());
    }

    private sealed class ApprovedPricingVersionRow
    {
        public Guid ProductPricingVersionId { get; init; }
        public Guid ProductId { get; init; }
        public DateTime PriceDate { get; init; }
    }

    private sealed class StoredPricingTierRow
    {
        public Guid ProductPricingVersionId { get; init; }
        public string QuantityRangeLabel { get; init; } = string.Empty;
        public decimal? MinQuantity { get; init; }
        public decimal? MaxQuantity { get; init; }
        public bool MinInclusive { get; init; }
        public bool MaxInclusive { get; init; }
        public decimal UnitPrice { get; init; }
        public int SortOrder { get; init; }
    }

    private sealed class LatestQuotationLineRow
    {
        public Guid ProductId { get; init; }
        public Guid QuotationLineId { get; init; }
        public Guid QuotationId { get; init; }
        public DateTime SentDate { get; init; }
        public DateTime? QuotationUpdatedDate { get; init; }
        public int LineSortOrder { get; init; }
    }

    private sealed class LatestQuotationTierRow
    {
        public Guid QuotationLineId { get; init; }
        public string QuantityRangeLabel { get; init; } = string.Empty;
        public decimal? MinQuantity { get; init; }
        public decimal? MaxQuantity { get; init; }
        public bool MinInclusive { get; init; }
        public bool MaxInclusive { get; init; }
        public decimal UnitPrice { get; init; }
        public int SortOrder { get; init; }
    }
}

internal sealed record QuotationTierPriceReference(
    string QuantityRangeLabel,
    decimal? MinQuantity,
    decimal? MaxQuantity,
    bool MinInclusive,
    bool MaxInclusive,
    decimal UnitPrice,
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
