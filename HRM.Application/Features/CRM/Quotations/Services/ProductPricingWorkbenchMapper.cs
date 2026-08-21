using HRM.Application.Commons.Pricing.Dtos;
using HRM.Application.Commons.Pricing.Helpers;
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
        IReadOnlyList<ProductPricingRequestRow> requests)
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
        var hasApproved = approved is not null;
        IReadOnlyList<ProductPricingRequestRow> waitingRequests = hasApproved ? [] : requests;

        return new ProductPricingWorkbenchItemDto
        {
            ProductId = product.ProductId,
            ProductCode = product.ProductCode,
            ProductName = product.ProductName,
            Currency = currency,
            PricingStatus = draft is not null
                ? ProductPricingLookupStatus.Draft
                : approved is not null
                    ? ProductPricingLookupStatus.Approved
                    : source is not null
                        ? ProductPricingLookupStatus.Draft
                        : ProductPricingLookupStatus.NoEligibleSource,
            IsSystemCalculatedDraft = storedPricing is null && source is not null,
            WaitingQuotationCount = waitingRequests
                .Select(x => x.QuotationId)
                .Distinct()
                .Count(),
            LatestRequestedAt = waitingRequests.Count == 0
                ? null
                : waitingRequests.Max(x => x.RequestedAt),
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
            ManufacturingCost = effectivePricing?.ManufacturingCost ??
                storedPricing?.ManufacturingCost ??
                source?.ManufacturingCost,
            UsedDefaultManufacturingCost = effectivePricing?.UsedDefaultManufacturingCost ??
                source?.UsedDefaultManufacturingCost == true,
            StandardSellingPrice = effectivePricing?.StandardSellingPrice ??
                storedPricing?.StandardSellingPrice ??
                source?.StandardSellingPrice,
            ProfitMarginRate = effectivePricing?.ProfitMarginRate ??
                storedPricing?.ProfitMarginRate ??
                source?.ProfitMarginRate,
            DraftPricingVersionId = draft?.ProductPricingVersionId,
            ApprovedPricingVersionId = approved?.ProductPricingVersionId,
            PricingUpdatedDate = storedPricing?.UpdatedDate ??
                storedPricing?.CreatedDate ??
                source?.UpdatedDate
        };
    }

    public static FormulaPriceCalculationDto? BuildEffectivePricing(
        PricingVersionRow? storedPricing,
        ProductPricingSourceOptionDto? source)
    {
        if (source?.Pricing is null ||
            !source.IsCurrentMaterialCostComplete ||
            !source.CurrentMaterialCost.HasValue)
        {
            return null;
        }

        return FormulaPriceCalculator.Calculate(
            source.Pricing.Profile,
            source.CurrentMaterialCost.Value,
            storedPricing?.ManufacturingCost ?? source.ManufacturingCost,
            storedPricing?.StandardSellingPrice);
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
