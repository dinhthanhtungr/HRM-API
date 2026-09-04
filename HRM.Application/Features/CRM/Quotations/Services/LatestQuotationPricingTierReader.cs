using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Domain.Entities.CustomerSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Services;

internal sealed class LatestQuotationPricingTierReader
{
    private readonly ICRMReadDbContext _dbContext;

    public LatestQuotationPricingTierReader(ICRMReadDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyDictionary<Guid, LatestQuotedTierPricingReference>> LoadAsync(
        IReadOnlyCollection<Guid> productIds,
        Guid customerId,
        string currency,
        Guid? excludedQuotationId,
        IQueryable<Quotation> visibleQuotations,
        CancellationToken cancellationToken)
    {
        if (productIds.Count == 0 || customerId == Guid.Empty)
        {
            return new Dictionary<Guid, LatestQuotedTierPricingReference>();
        }

        var candidates = await visibleQuotations
            .Where(x =>
                (!excludedQuotationId.HasValue || x.QuotationId != excludedQuotationId.Value) &&
                x.CustomerId == customerId &&
                x.Currency == currency &&
                x.SentDate.HasValue)
            .SelectMany(quotation => quotation.Lines
                .Where(line =>
                    productIds.Contains(line.ProductId) &&
                    line.IsActive &&
                    line.PriceTiers.Any(tier => tier.IsActive))
                .Select(line => new LatestQuotationLineRow
                {
                    ProductId = line.ProductId,
                    QuotationLineId = line.QuotationLineId,
                    QuotationId = quotation.QuotationId,
                    QuotationExternalId = quotation.ExternalId,
                    QuotationDate = quotation.QuotationDate,
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
        var lineById = latestLines.ToDictionary(x => x.QuotationLineId);
        var lineIds = lineById.Keys.ToArray();
        if (lineIds.Length == 0)
        {
            return new Dictionary<Guid, LatestQuotedTierPricingReference>();
        }

        var tierRows = await _dbContext.QuotationLinePriceTiers
            .AsNoTracking()
            .Where(x => lineIds.Contains(x.QuotationLineId) && x.IsActive)
            .Select(x => new LatestQuotationTierRow
            {
                QuotationLineId = x.QuotationLineId,
                QuantityRangeLabel = x.QuantityRangeLabel,
                MinQuantity = x.MinQuantity,
                MaxQuantity = x.MaxQuantity,
                MinInclusive = x.MinInclusive,
                MaxInclusive = x.MaxInclusive,
                UnitPrice = x.CustomerUnitPrice,
                SortOrder = x.SortOrder
            })
            .ToListAsync(cancellationToken);

        var tiersByLine = tierRows
            .GroupBy(x => x.QuotationLineId)
            .ToDictionary(
                x => x.Key,
                x => (IReadOnlyList<QuotationTierPriceReference>)x
                    .OrderBy(tier => tier.SortOrder)
                    .Select(tier => new QuotationTierPriceReference(
                        tier.QuantityRangeLabel,
                        tier.MinQuantity,
                        tier.MaxQuantity,
                        tier.MinInclusive,
                        tier.MaxInclusive,
                        tier.UnitPrice,
                        tier.SortOrder,
                        lineById[tier.QuotationLineId].SentDate))
                    .ToArray());

        return latestLines.ToDictionary(
            x => x.ProductId,
            x => new LatestQuotedTierPricingReference(
                x.ProductId,
                x.QuotationId,
                x.QuotationExternalId,
                x.QuotationDate,
                x.SentDate,
                tiersByLine.GetValueOrDefault(x.QuotationLineId) ?? []));
    }

    private sealed class LatestQuotationLineRow
    {
        public Guid ProductId { get; init; }
        public Guid QuotationLineId { get; init; }
        public Guid QuotationId { get; init; }
        public string QuotationExternalId { get; init; } = string.Empty;
        public DateTime QuotationDate { get; init; }
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
