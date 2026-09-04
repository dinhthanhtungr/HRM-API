using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Domain.Enums.CustomerEnum;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Services;

internal sealed class ApprovedProductPricingTierReader
{
    private readonly ICRMReadDbContext _dbContext;

    public ApprovedProductPricingTierReader(ICRMReadDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyDictionary<Guid, ApprovedProductTierPricingReference>> LoadAsync(
        IReadOnlyCollection<Guid> productIds,
        Guid companyId,
        string currency,
        CancellationToken cancellationToken)
    {
        if (productIds.Count == 0)
        {
            return new Dictionary<Guid, ApprovedProductTierPricingReference>();
        }

        var versions = await _dbContext.ProductPricingVersions
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == companyId &&
                productIds.Contains(x.ProductId) &&
                x.Currency == currency &&
                x.Status == ProductPricingStatus.Approved &&
                x.IsActive)
            .OrderByDescending(x => x.Version)
            .ThenByDescending(x => x.ApprovedAt ?? x.UpdatedDate ?? x.CreatedDate)
            .Select(x => new ApprovedVersionRow
            {
                ProductPricingVersionId = x.ProductPricingVersionId,
                ProductId = x.ProductId,
                Version = x.Version,
                Status = x.Status,
                ApprovedAt = x.ApprovedAt,
                StandardSellingPrice = x.StandardSellingPrice,
                PriceDate = x.ApprovedAt ?? x.UpdatedDate ?? x.CreatedDate,
                SourceType = x.SourceManufacturingFormulaId.HasValue
                    ? ProductPricingSourceType.ManufacturingFormula
                    : ProductPricingSourceType.Formula,
                SourceId = x.SourceManufacturingFormulaId ?? x.SourceFormulaId ?? Guid.Empty,
                SourceExternalId = x.FormulaExternalIdSnapshot ?? string.Empty,
                SourceName = x.SourceManufacturingFormula != null
                    ? x.SourceManufacturingFormula.Name
                    : x.SourceFormula != null
                        ? x.SourceFormula.Name
                        : string.Empty
            })
            .ToListAsync(cancellationToken);

        var latestVersions = versions
            .GroupBy(x => x.ProductId)
            .Select(x => x.First())
            .ToArray();
        var versionById = latestVersions.ToDictionary(x => x.ProductPricingVersionId);
        var versionIds = versionById.Keys.ToArray();

        var tiers = versionIds.Length == 0
            ? []
            : await _dbContext.ProductPricingTiers
                .AsNoTracking()
                .Where(x => versionIds.Contains(x.ProductPricingVersionId) && x.IsActive)
                .Select(x => new ApprovedTierRow
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

        var tiersByVersion = tiers
            .GroupBy(x => x.ProductPricingVersionId)
            .ToDictionary(
                x => x.Key,
                x => (IReadOnlyList<QuotationTierPriceReference>)x
                    .OrderBy(tier => tier.SortOrder)
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
                    .ToArray());

        return latestVersions.ToDictionary(
            x => x.ProductId,
            x => new ApprovedProductTierPricingReference(
                x.ProductPricingVersionId,
                x.ProductId,
                x.Version,
                x.Status,
                x.ApprovedAt,
                x.StandardSellingPrice,
                x.SourceType,
                x.SourceId,
                x.SourceExternalId,
                x.SourceName,
                tiersByVersion.GetValueOrDefault(x.ProductPricingVersionId) ?? []));
    }

    private sealed class ApprovedVersionRow
    {
        public Guid ProductPricingVersionId { get; init; }
        public Guid ProductId { get; init; }
        public int Version { get; init; }
        public ProductPricingStatus Status { get; init; }
        public DateTime? ApprovedAt { get; init; }
        public decimal? StandardSellingPrice { get; init; }
        public DateTime PriceDate { get; init; }
        public ProductPricingSourceType SourceType { get; init; }
        public Guid SourceId { get; init; }
        public string SourceExternalId { get; init; } = string.Empty;
        public string SourceName { get; init; } = string.Empty;
    }

    private sealed class ApprovedTierRow
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
}
