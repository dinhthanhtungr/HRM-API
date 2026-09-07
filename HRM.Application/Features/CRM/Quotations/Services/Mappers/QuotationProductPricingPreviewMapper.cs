using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Features.CRM.Quotations.Services;

internal static class QuotationProductPricingPreviewMapper
{
    public static QuotationProductPricingLinePreviewDto Map(
        Guid productId,
        string productCode,
        string productName,
        string unit,
        string currency,
        ResolvedProductTierPricingReferences references,
        QuotationPricingAvailability pricingAvailability,
        bool canUseManualCustomerPrice,
        string? warningCode,
        string? warningMessage,
        IReadOnlyList<QuotationTierPriceReference> manualPriceTierTemplates)
    {
        var defaultTiers = references.DefaultPriceTiers;
        var displayTiers = defaultTiers.Count > 0
            ? defaultTiers
            : manualPriceTierTemplates;
        var latestTiers = references.LatestQuotedPricing?.PriceTiers ?? [];
        var approved = references.ApprovedPricing;

        return new QuotationProductPricingLinePreviewDto
        {
            ProductId = productId,
            ProductExternalId = productCode,
            ProductName = productName,
            Currency = currency,
            ProductPricingVersionId = approved?.ProductPricingVersionId,
            ProductPricingVersion = approved?.Version,
            ProductPricingStatus = approved?.Status,
            HasApprovedPricingAvailable = approved is not null,
            HasNewerPricingVersion = false,
            CanApplyToQuotation =
                defaultTiers.Count > 0 &&
                defaultTiers.All(x => x.UnitPrice.HasValue),
            CanUseManualCustomerPrice = canUseManualCustomerPrice,
            PricingAvailability = pricingAvailability,
            WarningCode = warningCode,
            WarningMessage = warningMessage,
            DefaultPriceTierSource = references.DefaultPriceTierSource,
            ApprovedPricing = MapApproved(approved),
            SystemCalculatedPricing = MapSystem(references.SystemCalculatedPricing),
            LatestQuotedPricing = MapLatest(references.LatestQuotedPricing),
            ManualPriceTierTemplates = manualPriceTierTemplates
                .OrderBy(x => x.SortOrder)
                .Select(MapReferenceTier)
                .ToArray(),
            Quantity = 0m,
            Unit = unit,
            PriceMode = references.DefaultPriceTierSource switch
            {
                QuotationDefaultPriceTierSource.ApprovedPricingVersion =>
                    QuotationLinePriceMode.ApprovedPricingEditable,
                QuotationDefaultPriceTierSource.SystemCalculated =>
                    QuotationLinePriceMode.FormulaCalculatedEditable,
                _ => QuotationLinePriceMode.ManualAuthorized
            },
            UnitPrice = 0m,
            DiscountPercent = 0m,
            LineTotal = 0m,
            PriceTiers = displayTiers
                .OrderBy(x => x.SortOrder)
                .Select(x =>
                {
                    var matchTarget = new QuotationLinePriceTierDto
                    {
                        QuantityRangeLabel = x.QuantityRangeLabel,
                        MinQuantity = x.MinQuantity,
                        MaxQuantity = x.MaxQuantity,
                        MinInclusive = x.MinInclusive,
                        MaxInclusive = x.MaxInclusive,
                        UnitPrice = x.UnitPrice ?? 0m,
                        CommissionAmount = 0m,
                        CustomerUnitPrice = x.UnitPrice ?? 0m,
                        SortOrder = x.SortOrder
                    };
                    var latest = QuotationTierPriceMatcher.Find(latestTiers, matchTarget);
                    return new QuotationProductPricingTierPreviewDto
                    {
                        IsSnapshot = false,
                        RequiresManualPrice = defaultTiers.Count == 0 || !x.UnitPrice.HasValue,
                        QuantityRangeLabel = x.QuantityRangeLabel,
                        MinQuantity = x.MinQuantity,
                        MaxQuantity = x.MaxQuantity,
                        MinInclusive = x.MinInclusive,
                        MaxInclusive = x.MaxInclusive,
                        UnitPrice = x.UnitPrice ?? 0m,
                        CommissionAmount = 0m,
                        CustomerUnitPrice = x.UnitPrice ?? 0m,
                        StandardUnitPrice = x.UnitPrice,
                        StandardPriceUpdatedDate = x.PriceDate,
                        LatestQuotedUnitPrice = latest?.UnitPrice,
                        LatestQuotedDate = latest?.PriceDate,
                        SortOrder = x.SortOrder
                    };
                })
                .ToArray(),
            SortOrder = 0
        };
    }

    private static QuotationReferencePriceTierDto MapReferenceTier(
        QuotationTierPriceReference tier)
        => new()
        {
            QuantityRangeLabel = tier.QuantityRangeLabel,
            MinQuantity = tier.MinQuantity,
            MaxQuantity = tier.MaxQuantity,
            MinInclusive = tier.MinInclusive,
            MaxInclusive = tier.MaxInclusive,
            UnitPrice = tier.UnitPrice,
            RequiresManualPrice = !tier.UnitPrice.HasValue,
            SortOrder = tier.SortOrder
        };

    internal static QuotationApprovedTierPricingDto? MapApproved(
        ApprovedProductTierPricingReference? pricing)
        => pricing is null
            ? null
            : new QuotationApprovedTierPricingDto
            {
                ProductPricingVersionId = pricing.ProductPricingVersionId,
                Version = pricing.Version,
                Status = pricing.Status,
                ApprovedAt = pricing.ApprovedAt,
                StandardSellingPrice = pricing.StandardSellingPrice,
                Currency = pricing.Currency,
                SourceType = pricing.SourceType,
                SourceId = pricing.SourceId,
                SourceExternalId = pricing.SourceExternalId,
                SourceName = pricing.SourceName,
                PriceTiers = MapReferenceTiers(pricing.PriceTiers)
            };

    internal static QuotationSystemCalculatedTierPricingDto? MapSystem(
        SystemCalculatedTierPricingReference? pricing)
        => pricing is null
            ? null
            : new QuotationSystemCalculatedTierPricingDto
            {
                PricingStatus = pricing.PricingStatus,
                CalculatedAt = pricing.CalculatedAt,
                StandardSellingPrice = pricing.StandardSellingPrice,
                Currency = pricing.Currency,
                SourceType = pricing.SourceType,
                SourceId = pricing.SourceId,
                SourceExternalId = pricing.SourceExternalId,
                SourceName = pricing.SourceName,
                PriceTiers = MapReferenceTiers(pricing.PriceTiers)
            };

    internal static QuotationLatestQuotedTierPricingDto? MapLatest(
        LatestQuotedTierPricingReference? pricing)
        => pricing is null
            ? null
            : new QuotationLatestQuotedTierPricingDto
            {
                Currency = pricing.Currency,
                QuotationId = pricing.QuotationId,
                QuotationExternalId = pricing.QuotationExternalId,
                QuotationDate = pricing.QuotationDate,
                SentDate = pricing.SentDate,
                PriceTiers = MapReferenceTiers(pricing.PriceTiers)
            };

    private static IReadOnlyList<QuotationReferencePriceTierDto> MapReferenceTiers(
        IReadOnlyList<QuotationTierPriceReference> tiers)
        => tiers
            .OrderBy(x => x.SortOrder)
            .Select(x => new QuotationReferencePriceTierDto
            {
                QuantityRangeLabel = x.QuantityRangeLabel,
                MinQuantity = x.MinQuantity,
                MaxQuantity = x.MaxQuantity,
                MinInclusive = x.MinInclusive,
                MaxInclusive = x.MaxInclusive,
                UnitPrice = x.UnitPrice,
                RequiresManualPrice = !x.UnitPrice.HasValue,
                SortOrder = x.SortOrder
            })
            .ToArray();
}
