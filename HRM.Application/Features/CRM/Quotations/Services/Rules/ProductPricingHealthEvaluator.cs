using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Features.CRM.Quotations.Services;

/// <summary>
/// Xác định liệu dữ liệu giá hiện tại có còn đủ tin cậy để sử dụng hay cần President rà soát lại.
/// Ngưỡng được lấy từ QuotationFeatureOptions để tránh rải số ngày trong các query.
/// </summary>
internal static class ProductPricingHealthEvaluator
{
    public static ProductPricingHealthResult Evaluate(
        ProductPricingSourceOptionDto? source,
        PricingVersionRow? draft,
        PricingVersionRow? approved,
        DateTime now,
        QuotationFeatureOptions options,
        DateTime? latestFormulaConfirmedAt = null)
    {
        var reviewDueDate = CalculateReviewDueDate(approved, options);
        var confirmedAt = GetConfirmedAt(approved);
        var hasFormulaConfirmedSinceApproval = approved is not null &&
            latestFormulaConfirmedAt.HasValue &&
            latestFormulaConfirmedAt.Value > confirmedAt!.Value;
        var isReviewExpired = reviewDueDate.HasValue && now >= reviewDueDate.Value;
        var requiresReapproval = approved is not null &&
            (hasFormulaConfirmedSinceApproval || isReviewExpired);
        ProductPricingHealthResult PendingReapproval() => new(
            ProductPricingHealthStatus.PendingReapproval,
            true,
            reviewDueDate,
            ProductStandardPriceState.PendingReapproval,
            hasFormulaConfirmedSinceApproval,
            isReviewExpired);

        // Pending reapproval is a business state of the already-published price;
        // it must remain visible even if its historical source is later invalid.
        if (requiresReapproval)
        {
            return PendingReapproval();
        }

        // The approved pricing version is the canonical record of whether a product has
        // a standard price. Technical-source and policy problems still require action,
        // but must not make an existing approved price look missing in list views.
        var standardPriceState = approved is null
            ? ProductStandardPriceState.PendingInitialApproval
            : ProductStandardPriceState.Active;

        if (source is null)
        {
            return new ProductPricingHealthResult(
                ProductPricingHealthStatus.NoEligibleSource,
                true,
                null,
                standardPriceState);
        }

        if (!source.IsEligible)
        {
            return new ProductPricingHealthResult(
                ProductPricingHealthStatus.SourceNoLongerEligible,
                true,
                null,
                standardPriceState);
        }

        if (source.PricingStatus == FormulaPricingPolicyRules.PricingPolicyMissing)
        {
            return new ProductPricingHealthResult(
                ProductPricingHealthStatus.PricingPolicyMissing,
                true,
                null,
                standardPriceState);
        }

        if (!source.IsCurrentMaterialCostComplete)
        {
            return new ProductPricingHealthResult(
                ProductPricingHealthStatus.MissingMaterialPrice,
                false,
                reviewDueDate,
                standardPriceState);
        }

        var storedPricing = draft ?? approved;
        var materialCostDifferencePercent = CalculateMaterialCostDifferencePercent(
            source.CurrentMaterialCost,
            storedPricing?.MaterialCostSnapshot);
        if (options.MaterialCostChangeThresholdPercent > 0m &&
            materialCostDifferencePercent.HasValue &&
            materialCostDifferencePercent.Value >= options.MaterialCostChangeThresholdPercent)
        {
            return new ProductPricingHealthResult(
                ProductPricingHealthStatus.MaterialCostChanged,
                true,
                reviewDueDate,
                standardPriceState);
        }

        var storedPricingDate = storedPricing?.UpdatedDate ?? storedPricing?.CreatedDate;
        if (options.CostingStaleAfterDays > 0 &&
            storedPricingDate.HasValue &&
            now >= storedPricingDate.Value.AddDays(options.CostingStaleAfterDays))
        {
            return new ProductPricingHealthResult(
                ProductPricingHealthStatus.CostingStale,
                true,
                reviewDueDate,
                standardPriceState);
        }

        return approved is null
            ? new ProductPricingHealthResult(
                ProductPricingHealthStatus.AwaitingApproval,
                true,
                null,
                ProductStandardPriceState.PendingInitialApproval)
            : new ProductPricingHealthResult(
                ProductPricingHealthStatus.Ready,
                false,
                reviewDueDate,
                ProductStandardPriceState.Active);
    }

    private static decimal? CalculateMaterialCostDifferencePercent(
        decimal? currentMaterialCost,
        decimal? storedMaterialCost)
        => currentMaterialCost.HasValue && storedMaterialCost is > 0m
            ? decimal.Round(
                (currentMaterialCost.Value - storedMaterialCost.Value) /
                storedMaterialCost.Value * 100m,
                4,
                MidpointRounding.AwayFromZero)
            : null;

    private static DateTime? CalculateReviewDueDate(
        PricingVersionRow? approved,
        QuotationFeatureOptions options)
    {
        if (approved is null || options.ApprovedPricingReviewAfterDays <= 0)
        {
            return null;
        }

        var approvedAt = GetConfirmedAt(approved);
        return approvedAt!.Value.AddDays(options.ApprovedPricingReviewAfterDays);
    }

    private static DateTime? GetConfirmedAt(PricingVersionRow? approved)
        => approved?.ApprovedAt ?? approved?.UpdatedDate ?? approved?.CreatedDate;
}

internal sealed record ProductPricingHealthResult(
    ProductPricingHealthStatus Status,
    bool RequiresPricingAction,
    DateTime? PricingReviewDueDate,
    ProductStandardPriceState StandardPriceState,
    bool HasFormulaConfirmationPending = false,
    bool IsReviewExpired = false);
