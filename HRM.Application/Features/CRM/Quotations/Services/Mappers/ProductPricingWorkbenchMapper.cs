using HRM.Application.Commons.Pricing.Dtos;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Features.CRM.Quotations.Services;

internal static class ProductPricingWorkbenchMapper
{
    public static ProductPricingWorkbenchItemDto MapSummary(
        ProductRow product,
        string currency,
        PricingVersionRow? draft,
        PricingVersionRow? approved,
        ProductPricingSourceOptionDto? source,
        IReadOnlyList<ProductPricingRequestRow> requests,
        IReadOnlyList<ProductPricingWorkbenchCustomerContextDto>? relatedCustomers = null,
        ProductPricingHealthResult? health = null,
        DateTime? now = null)
    {
        var storedPricing = draft ?? approved;
        var effectivePricing = BuildEffectivePricing(storedPricing, source);
        var currentMaterialCost = source?.CurrentMaterialCost;
        var storedMaterialCost = storedPricing?.MaterialCostSnapshot;
        decimal? difference = currentMaterialCost.HasValue && storedMaterialCost.HasValue
            ? decimal.Round(
                currentMaterialCost.Value - storedMaterialCost.Value,
                6,
                MidpointRounding.AwayFromZero)
            : null;
        decimal? differencePercent = difference.HasValue && storedMaterialCost is > 0m
            ? decimal.Round(
                difference.Value / storedMaterialCost.GetValueOrDefault() * 100m,
                4,
                MidpointRounding.AwayFromZero)
            : null;
        var storedStandardSellingPrice = storedPricing?.StandardSellingPrice;
        var realtimeStandardSellingPrice = effectivePricing?.StandardSellingPrice ??
            source?.StandardSellingPrice;
        var hasRealtimePriceComparison = storedStandardSellingPrice is > 0m &&
            realtimeStandardSellingPrice.HasValue;
        decimal? standardSellingPriceDifference = hasRealtimePriceComparison
            ? decimal.Round(
                realtimeStandardSellingPrice!.Value - storedStandardSellingPrice!.Value,
                6,
                MidpointRounding.AwayFromZero)
            : null;
        decimal? standardSellingPriceDifferencePercent = hasRealtimePriceComparison
            ? decimal.Round(
                standardSellingPriceDifference!.Value / storedStandardSellingPrice!.Value * 100m,
                4,
                MidpointRounding.AwayFromZero)
            : null;
        var hasApproved = approved is not null;
        IReadOnlyList<ProductPricingRequestRow> waitingRequests = hasApproved ? [] : requests;

        DateTime? expiresAt = approved?.ApprovedAt.HasValue == true && approved.PriceValidityDays is > 0
            ? approved.ApprovedAt.Value.AddDays(approved.PriceValidityDays.Value)
            : null;
        var today = (now ?? DateTime.Now).Date;
        int? remainingDays = expiresAt.HasValue
            ? Math.Max(0, (int)Math.Ceiling((expiresAt.Value.Date - today).TotalDays))
            : null;
        int? overdueDays = expiresAt.HasValue && expiresAt.Value.Date < today
            ? (int)Math.Floor((today - expiresAt.Value.Date).TotalDays)
            : null;

        return new ProductPricingWorkbenchItemDto
        {
            ProductId = product.ProductId,
            ProductCode = product.ProductCode,
            ProductName = product.ProductName,
            Currency = currency,
            PricingStatus = source?.PricingStatus == FormulaPricingPolicyRules.PricingPolicyMissing
                ? ProductPricingLookupStatus.PricingPolicyMissing
                : draft is not null
                ? ProductPricingLookupStatus.Draft
                : approved is not null
                    ? ProductPricingLookupStatus.Approved
                    : source is not null
                        ? ProductPricingLookupStatus.Draft
                        : ProductPricingLookupStatus.NoEligibleSource,
            IsSystemCalculatedDraft = storedPricing is null && source is not null,
            PricingHealthStatus = health?.Status ?? ProductPricingHealthStatus.Unknown,
            RequiresPricingAction = health?.RequiresPricingAction ?? false,
            PricingReviewDueDate = health?.PricingReviewDueDate,
            WaitingQuotationCount = waitingRequests
                .Select(x => x.QuotationId)
                .Distinct()
                .Count(),
            LatestRequestedAt = waitingRequests.Count == 0
                ? null
                : waitingRequests.Max(x => x.RequestedAt),
            RelatedCustomers = relatedCustomers ?? [],
            SourceType = source?.SourceType ?? storedPricing?.SourceType,
            SourceId = source?.SourceId ?? storedPricing?.SourceId,
            SourceExternalId = source?.ExternalId ?? storedPricing?.SourceExternalId,
            SourceName = source?.Name,
            SourceStatus = source?.Status,
            SourceIsEligible = source?.IsEligible == true,
            SourceIsCustomerSelected = source?.IsCustomerSelected == true,
            CurrentMaterialCost = currentMaterialCost,
            IsCurrentMaterialCostComplete = source?.IsCurrentMaterialCostComplete == true,
            MissingMaterialPriceCount = source?.MissingMaterialPriceCount ?? 0,
            StoredMaterialCostSnapshot = storedMaterialCost,
            MaterialCostDifference = difference,
            MaterialCostDifferencePercent = differencePercent,
            // The drawer's three editable values must come from one persisted version
            // together. Realtime pricing remains a comparison/preview only.
            ManufacturingCost = storedPricing?.ManufacturingCost ??
                effectivePricing?.ManufacturingCost ??
                source?.ManufacturingCost,
            UsedDefaultManufacturingCost = effectivePricing?.UsedDefaultManufacturingCost ??
                source?.UsedDefaultManufacturingCost == true,
            StandardSellingPrice = storedStandardSellingPrice ?? realtimeStandardSellingPrice,
            RealtimeStandardSellingPrice = realtimeStandardSellingPrice,
            StandardSellingPriceDifference = standardSellingPriceDifference,
            StandardSellingPriceDifferencePercent = standardSellingPriceDifferencePercent,
            HasRealtimePriceComparison = hasRealtimePriceComparison,
            ProfitMarginRate = storedPricing?.ProfitMarginRate ??
                effectivePricing?.ProfitMarginRate ??
                source?.ProfitMarginRate,
            DraftPricingVersionId = draft?.ProductPricingVersionId,
            ApprovedPricingVersionId = approved?.ProductPricingVersionId,
            PricingUpdatedDate = storedPricing?.UpdatedDate ??
                storedPricing?.CreatedDate ??
                source?.UpdatedDate,
            PriceConfirmedAt = approved?.ApprovedAt,
            PriceExpiresAt = expiresAt,
            RemainingValidityDays = remainingDays,
            OverdueDays = overdueDays,
            IsPriceExpired = expiresAt.HasValue ? expiresAt.Value.Date < today : null
        };
    }

    public static FormulaPriceCalculationDto? BuildEffectivePricing(
        PricingVersionRow? storedPricing,
        ProductPricingSourceOptionDto? source)
    {
        // Source pricing is the canonical engine calculation. This mapper must not recalculate it.
        return source?.Pricing;
    }

    public static IReadOnlyList<QuotationPricingWorkspaceTierDto> MapStoredTiers(
        IReadOnlyList<ProductPricingTier> tiers,
        FormulaPriceCalculationDto? pricing)
        => tiers.Select(x => new QuotationPricingWorkspaceTierDto
        {
            QuantityRangeLabel = x.QuantityRangeLabel,
            MinQuantity = x.MinQuantity,
            MaxQuantity = x.MaxQuantity,
            MinInclusive = x.MinInclusive,
            MaxInclusive = x.MaxInclusive,
            UnitPrice = x.UnitPrice,
            MarginVsMaterialPercent = CalculateMargin(x.UnitPrice, pricing?.MaterialCost),
            MarginVsCostPercent = CalculateMargin(x.UnitPrice, pricing?.CostBase),
            RequiresManualPrice = false,
            IsStored = true,
            SortOrder = x.SortOrder
        }).ToArray();

    public static IReadOnlyList<QuotationPricingWorkspaceTierDto> MapSuggestedTiers(
        IReadOnlyList<FormulaSuggestedPriceTierDto> tiers)
        => tiers.Select(x => new QuotationPricingWorkspaceTierDto
        {
            QuantityRangeLabel = x.QuantityRangeLabel,
            MinQuantity = x.MinQuantity,
            MaxQuantity = x.MaxQuantity,
            MinInclusive = x.MinInclusive,
            MaxInclusive = x.MaxInclusive,
            UnitPrice = x.UnitPrice,
            MarginVsMaterialPercent = x.MarginVsMaterialPercent,
            MarginVsCostPercent = x.MarginVsCostPercent,
            RequiresManualPrice = x.RequiresManualPrice,
            IsStored = false,
            SortOrder = x.SortOrder
        }).ToArray();

    private static decimal? CalculateMargin(decimal price, decimal? comparisonBase)
    {
        if (!comparisonBase.HasValue || comparisonBase <= 0m || price <= 0m)
        {
            return null;
        }

        return decimal.Round(
            (price - comparisonBase.Value) / comparisonBase.Value * 100m,
            4,
            MidpointRounding.AwayFromZero);
    }
}
