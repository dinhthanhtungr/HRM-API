using HRM.Application.Features.Pricing.Authorization;
using HRM.Application.Features.CRM.Quotations.Dtos;

namespace HRM.Application.Features.CRM.Quotations.Services;

internal static class ProductPricingWorkbenchVisibility
{
    // Compatibility overload for read models that have not yet been migrated to capability decisions.
    public static ProductPricingWorkbenchItemDto ApplyToSummary(
        ProductPricingWorkbenchItemDto source,
        bool canManagePricing)
        => ApplyToSummary(source, LegacyAccess(canManagePricing));

    public static ProductPricingWorkbenchItemDto ApplyToSummary(
        ProductPricingWorkbenchItemDto source,
        PricingAccessDecision access)
        => CopySummary(source, access);

    public static ProductPricingWorkbenchDetailDto ApplyToDetail(
        ProductPricingWorkbenchDetailDto source,
        PricingAccessDecision access)
        => new()
        {
            Summary = ApplyToSummary(source.Summary, access),
            ManufacturingCost = access.CanViewManufacturingCost
                ? source.ManufacturingCost
                : null,
            StandardSellingPrice = access.CanViewApprovedSellingPrice
                ? source.StandardSellingPrice
                : null,
            ProfitMarginRate = access.CanViewMargin ? source.ProfitMarginRate : null,
            SelectedSource = source.SelectedSource is null
                ? null
                : ProductPricingSourceVisibility.Apply(source.SelectedSource, access),
            DraftPricing = access.CanManage ? source.DraftPricing : null,
            ApprovedPricing = access.CanViewHistory ? source.ApprovedPricing : null,
            DisplayPriceTiers = access.CanViewApprovedSellingPrice ||
                                access.CanViewSystemCalculatedPrice
                ? ToVisibleTiers(source.DisplayPriceTiers, access)
                : [],
            PricingHistory = access.CanViewHistory ? source.PricingHistory : [],
            RelatedQuotations = access.CanManage ? source.RelatedQuotations : []
        };

    private static ProductPricingWorkbenchItemDto CopySummary(
        ProductPricingWorkbenchItemDto source,
        PricingAccessDecision access)
        => new()
        {
            CanOpenPricingDetail = access.CanViewWorkbench,
            CanManagePricing = access.CanManage,
            ProductId = source.ProductId,
            ProductCode = source.ProductCode,
            ProductName = source.ProductName,
            Currency = source.Currency,
            PricingStatus = source.PricingStatus,
            IsSystemCalculatedDraft = source.IsSystemCalculatedDraft,
            PricingHealthStatus = source.PricingHealthStatus,
            RequiresPricingAction = source.RequiresPricingAction,
            PricingReviewDueDate = source.PricingReviewDueDate,
            StandardPriceState = source.StandardPriceState,
            HasFormulaConfirmationPending = access.CanManage && source.HasFormulaConfirmationPending,
            IsPricingReviewExpired = source.IsPricingReviewExpired,
            WaitingQuotationCount = source.WaitingQuotationCount,
            LatestRequestedAt = access.CanManage ? source.LatestRequestedAt : null,
            RelatedCustomers = access.CanManage
                ? source.RelatedCustomers
                : ToSaleRelatedCustomers(source.RelatedCustomers),
            SourceType = source.SourceType,
            SourceId = source.SourceId,
            SourceExternalId = source.SourceExternalId,
            SourceName = source.SourceName,
            SourceStatus = source.SourceStatus,
            SourceIsEligible = source.SourceIsEligible,
            SourceIsCustomerSelected = access.CanManage && source.SourceIsCustomerSelected,
            CurrentMaterialCost = access.CanViewMaterialCost ? source.CurrentMaterialCost : null,
            IsCurrentMaterialCostComplete = access.CanViewMaterialCost && source.IsCurrentMaterialCostComplete,
            MissingMaterialPriceCount = access.CanViewMaterialCost ? source.MissingMaterialPriceCount : 0,
            StoredMaterialCostSnapshot = access.CanViewMaterialCost ? source.StoredMaterialCostSnapshot : null,
            MaterialCostDifference = access.CanViewMaterialCost ? source.MaterialCostDifference : null,
            MaterialCostDifferencePercent = access.CanViewMaterialCost
                ? source.MaterialCostDifferencePercent
                : null,
            ManufacturingCost = access.CanViewManufacturingCost ? source.ManufacturingCost : null,
            UsedDefaultManufacturingCost = access.CanViewManufacturingCost && source.UsedDefaultManufacturingCost,
            StandardSellingPrice = access.CanViewApprovedSellingPrice ? source.StandardSellingPrice : null,
            PublisherNote = access.CanViewApprovedSellingPrice ? source.PublisherNote : null,
            RealtimeStandardSellingPrice = access.CanViewSystemCalculatedPrice
                ? source.RealtimeStandardSellingPrice
                : null,
            StandardSellingPriceDifference = access.CanViewSystemCalculatedPrice
                ? source.StandardSellingPriceDifference
                : null,
            StandardSellingPriceDifferencePercent = access.CanViewSystemCalculatedPrice
                ? source.StandardSellingPriceDifferencePercent
                : null,
            HasRealtimePriceComparison = access.CanViewSystemCalculatedPrice &&
                source.HasRealtimePriceComparison,
            ProfitMarginRate = access.CanViewMargin ? source.ProfitMarginRate : null,
            DraftPricingVersionId = access.CanViewHistory ? source.DraftPricingVersionId : null,
            ApprovedPricingVersionId = access.CanViewHistory ? source.ApprovedPricingVersionId : null,
            PricingUpdatedDate = access.CanViewHistory ? source.PricingUpdatedDate : null,
            PriceConfirmedAt = access.CanViewHistory ? source.PriceConfirmedAt : null,
            PriceExpiresAt = access.CanViewHistory ? source.PriceExpiresAt : null,
            RemainingValidityDays = access.CanViewHistory ? source.RemainingValidityDays : null,
            OverdueDays = access.CanViewHistory ? source.OverdueDays : null,
            IsPriceExpired = access.CanViewHistory ? source.IsPriceExpired : null
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

    private static IReadOnlyList<QuotationPricingWorkspaceTierDto> ToVisibleTiers(
        IReadOnlyList<QuotationPricingWorkspaceTierDto> tiers,
        PricingAccessDecision access)
        => tiers
            .Select(tier => new QuotationPricingWorkspaceTierDto
            {
                QuantityRangeLabel = tier.QuantityRangeLabel,
                MinQuantity = tier.MinQuantity,
                MaxQuantity = tier.MaxQuantity,
                MinInclusive = tier.MinInclusive,
                MaxInclusive = tier.MaxInclusive,
                UnitPrice = tier.UnitPrice,
                MarginVsMaterialPercent = access.CanViewMargin
                    ? tier.MarginVsMaterialPercent
                    : null,
                MarginVsCostPercent = access.CanViewMargin
                    ? tier.MarginVsCostPercent
                    : null,
                RequiresManualPrice = tier.RequiresManualPrice,
                IsStored = tier.IsStored,
                SortOrder = tier.SortOrder
            })
            .ToArray();

    private static PricingAccessDecision LegacyAccess(bool canManagePricing)
        => new(
            CanViewWorkbench: true,
            CanViewApprovedSellingPrice: true,
            CanViewSystemCalculatedPrice: canManagePricing,
            CanViewMaterialCost: canManagePricing,
            CanViewManufacturingCost: canManagePricing,
            CanViewMargin: canManagePricing,
            CanViewHistory: canManagePricing,
            CanManage: canManagePricing,
            CanApprove: canManagePricing);
}
