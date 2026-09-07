using HRM.Application.Features.CRM.Quotations.Dtos;

namespace HRM.Application.Features.CRM.Quotations.Services;

internal static class ProductPricingWorkbenchVisibility
{
    public static ProductPricingWorkbenchItemDto ApplyToSummary(
        ProductPricingWorkbenchItemDto source,
        bool canManagePricing)
    {
        if (canManagePricing)
        {
            return CopySummary(source, canManagePricing: true, includeSensitiveFields: true);
        }

        return CopySummary(source, canManagePricing: false, includeSensitiveFields: false);
    }

    public static ProductPricingWorkbenchDetailDto ApplyToDetail(
        ProductPricingWorkbenchDetailDto source,
        bool canManagePricing)
    {
        if (canManagePricing)
        {
            return new ProductPricingWorkbenchDetailDto
            {
                Summary = ApplyToSummary(source.Summary, canManagePricing: true),
                ManufacturingCost = source.ManufacturingCost,
                StandardSellingPrice = source.StandardSellingPrice,
                ProfitMarginRate = source.ProfitMarginRate,
                SelectedSource = source.SelectedSource,
                DraftPricing = source.DraftPricing,
                ApprovedPricing = source.ApprovedPricing,
                DisplayPriceTiers = source.DisplayPriceTiers,
                PricingHistory = source.PricingHistory,
                RelatedQuotations = source.RelatedQuotations
            };
        }

        return new ProductPricingWorkbenchDetailDto
        {
            Summary = ApplyToSummary(source.Summary, canManagePricing: false),
            StandardSellingPrice = source.StandardSellingPrice,
            SelectedSource = ToSaleSource(source.SelectedSource),
            DisplayPriceTiers = ToSaleTiers(source.DisplayPriceTiers)
        };
    }

    private static ProductPricingWorkbenchItemDto CopySummary(
        ProductPricingWorkbenchItemDto source,
        bool canManagePricing,
        bool includeSensitiveFields)
        => new()
        {
            CanOpenPricingDetail = true,
            CanManagePricing = canManagePricing,
            ProductId = source.ProductId,
            ProductCode = source.ProductCode,
            ProductName = source.ProductName,
            Currency = source.Currency,
            PricingStatus = source.PricingStatus,
            IsSystemCalculatedDraft = source.IsSystemCalculatedDraft,
            PricingHealthStatus = source.PricingHealthStatus,
            RequiresPricingAction = source.RequiresPricingAction,
            PricingReviewDueDate = source.PricingReviewDueDate,
            WaitingQuotationCount = source.WaitingQuotationCount,
            LatestRequestedAt = includeSensitiveFields ? source.LatestRequestedAt : null,
            RelatedCustomers = includeSensitiveFields
                ? source.RelatedCustomers
                : ToSaleRelatedCustomers(source.RelatedCustomers),
            SourceType = source.SourceType,
            SourceId = source.SourceId,
            SourceExternalId = source.SourceExternalId,
            SourceName = source.SourceName,
            SourceStatus = source.SourceStatus,
            SourceIsEligible = source.SourceIsEligible,
            SourceIsCustomerSelected = includeSensitiveFields && source.SourceIsCustomerSelected,
            CurrentMaterialCost = includeSensitiveFields ? source.CurrentMaterialCost : null,
            IsCurrentMaterialCostComplete = includeSensitiveFields && source.IsCurrentMaterialCostComplete,
            MissingMaterialPriceCount = includeSensitiveFields ? source.MissingMaterialPriceCount : 0,
            StoredMaterialCostSnapshot = includeSensitiveFields ? source.StoredMaterialCostSnapshot : null,
            MaterialCostDifference = includeSensitiveFields ? source.MaterialCostDifference : null,
            MaterialCostDifferencePercent = includeSensitiveFields
                ? source.MaterialCostDifferencePercent
                : null,
            ManufacturingCost = includeSensitiveFields ? source.ManufacturingCost : null,
            UsedDefaultManufacturingCost = includeSensitiveFields && source.UsedDefaultManufacturingCost,
            StandardSellingPrice = source.StandardSellingPrice,
            RealtimeStandardSellingPrice = includeSensitiveFields ? source.RealtimeStandardSellingPrice : null,
            StandardSellingPriceDifference = includeSensitiveFields ? source.StandardSellingPriceDifference : null,
            StandardSellingPriceDifferencePercent = includeSensitiveFields
                ? source.StandardSellingPriceDifferencePercent
                : null,
            HasRealtimePriceComparison = includeSensitiveFields && source.HasRealtimePriceComparison,
            ProfitMarginRate = includeSensitiveFields ? source.ProfitMarginRate : null,
            DraftPricingVersionId = includeSensitiveFields ? source.DraftPricingVersionId : null,
            ApprovedPricingVersionId = includeSensitiveFields ? source.ApprovedPricingVersionId : null,
            PricingUpdatedDate = includeSensitiveFields ? source.PricingUpdatedDate : null,
            PriceConfirmedAt = includeSensitiveFields ? source.PriceConfirmedAt : null,
            PriceExpiresAt = includeSensitiveFields ? source.PriceExpiresAt : null,
            RemainingValidityDays = includeSensitiveFields ? source.RemainingValidityDays : null,
            OverdueDays = includeSensitiveFields ? source.OverdueDays : null,
            IsPriceExpired = includeSensitiveFields ? source.IsPriceExpired : null
        };

    private static ProductPricingSourceOptionDto? ToSaleSource(
        ProductPricingSourceOptionDto? source)
        => source is null
            ? null
            : new ProductPricingSourceOptionDto
            {
                SourceType = source.SourceType,
                SourceId = source.SourceId,
                ExternalId = source.ExternalId,
                Name = source.Name
            };

    private static IReadOnlyList<ProductPricingWorkbenchCustomerContextDto> ToSaleRelatedCustomers(
        IReadOnlyList<ProductPricingWorkbenchCustomerContextDto> relatedCustomers)
        => relatedCustomers
            .Select(customer => new ProductPricingWorkbenchCustomerContextDto
            {
                CustomerId = customer.CustomerId,
                CustomerCode = customer.CustomerCode,
                CustomerName = customer.CustomerName,
                RelatedDocumentCount = customer.RelatedDocumentCount,
                LatestRelatedDate = customer.LatestRelatedDate,
                HealthSummary = null
            })
            .ToArray();

    private static IReadOnlyList<QuotationPricingWorkspaceTierDto> ToSaleTiers(
        IReadOnlyList<QuotationPricingWorkspaceTierDto> tiers)
        => tiers
            .Select(tier => new QuotationPricingWorkspaceTierDto
            {
                QuantityRangeLabel = tier.QuantityRangeLabel,
                MinQuantity = tier.MinQuantity,
                MaxQuantity = tier.MaxQuantity,
                MinInclusive = tier.MinInclusive,
                MaxInclusive = tier.MaxInclusive,
                UnitPrice = tier.UnitPrice,
                RequiresManualPrice = tier.RequiresManualPrice,
                IsStored = tier.IsStored,
                SortOrder = tier.SortOrder
            })
            .ToArray();
}
