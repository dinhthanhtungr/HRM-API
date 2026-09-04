using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Features.CRM.Quotations.Services;

internal sealed record ApprovedProductTierPricingReference(
    Guid ProductPricingVersionId,
    Guid ProductId,
    int Version,
    ProductPricingStatus Status,
    DateTime? ApprovedAt,
    decimal? StandardSellingPrice,
    ProductPricingSourceType SourceType,
    Guid SourceId,
    string SourceExternalId,
    string SourceName,
    IReadOnlyList<QuotationTierPriceReference> PriceTiers);

internal sealed record SystemCalculatedTierPricingReference(
    Guid ProductId,
    string PricingStatus,
    DateTime CalculatedAt,
    decimal? StandardSellingPrice,
    ProductPricingSourceType? SourceType,
    Guid? SourceId,
    string SourceExternalId,
    string SourceName,
    IReadOnlyList<QuotationTierPriceReference> PriceTiers);

internal sealed record LatestQuotedTierPricingReference(
    Guid ProductId,
    Guid QuotationId,
    string QuotationExternalId,
    DateTime QuotationDate,
    DateTime SentDate,
    IReadOnlyList<QuotationTierPriceReference> PriceTiers);

internal sealed record ResolvedProductTierPricingReferences(
    ApprovedProductTierPricingReference? ApprovedPricing,
    SystemCalculatedTierPricingReference? SystemCalculatedPricing,
    LatestQuotedTierPricingReference? LatestQuotedPricing,
    QuotationDefaultPriceTierSource? DefaultPriceTierSource)
{
    public IReadOnlyList<QuotationTierPriceReference> DefaultPriceTiers =>
        DefaultPriceTierSource == QuotationDefaultPriceTierSource.ApprovedPricingVersion
            ? ApprovedPricing?.PriceTiers ?? []
            : DefaultPriceTierSource == QuotationDefaultPriceTierSource.SystemCalculated
                ? SystemCalculatedPricing?.PriceTiers ?? []
                : [];
}
