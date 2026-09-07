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
        QuotationFeatureOptions options)
    {
        if (source is null)
        {
            return new ProductPricingHealthResult(
                ProductPricingHealthStatus.NoEligibleSource,
                true,
                null);
        }

        if (!source.IsEligible)
        {
            return new ProductPricingHealthResult(
                ProductPricingHealthStatus.SourceNoLongerEligible,
                true,
                null);
        }

        if (source.PricingStatus == FormulaPricingPolicyRules.PricingPolicyMissing)
        {
            return new ProductPricingHealthResult(
                ProductPricingHealthStatus.PricingPolicyMissing,
                true,
                null);
        }

        var reviewDueDate = CalculateReviewDueDate(approved, options);
        if (!source.IsCurrentMaterialCostComplete)
        {
            return new ProductPricingHealthResult(
                ProductPricingHealthStatus.MissingMaterialPrice,
                false,
                reviewDueDate);
        }

        var storedPricing = draft ?? approved;
        var materialCostDifferencePercent = CalculateMaterialCostDifferencePercent(
            source.CurrentMaterialCost,
            storedPricing?.MaterialCostSnapshot);
        if (options.MaterialCostChangeThresholdPercent > 0m &&
            materialCostDifferencePercent.HasValue &&
            Math.Abs(materialCostDifferencePercent.Value) >=
                options.MaterialCostChangeThresholdPercent)
        {
            return new ProductPricingHealthResult(
                ProductPricingHealthStatus.MaterialCostChanged,
                true,
                CalculateReviewDueDate(approved, options));
        }

        if (reviewDueDate.HasValue && now >= reviewDueDate.Value)
        {
            return new ProductPricingHealthResult(
                ProductPricingHealthStatus.RepricingRequired,
                true,
                reviewDueDate);
        }

        var storedPricingDate = storedPricing?.UpdatedDate ?? storedPricing?.CreatedDate;
        if (options.CostingStaleAfterDays > 0 &&
            storedPricingDate.HasValue &&
            now >= storedPricingDate.Value.AddDays(options.CostingStaleAfterDays))
        {
            return new ProductPricingHealthResult(
                ProductPricingHealthStatus.CostingStale,
                true,
                reviewDueDate);
        }

        return approved is null
            ? new ProductPricingHealthResult(
                ProductPricingHealthStatus.AwaitingApproval,
                true,
                null)
            : new ProductPricingHealthResult(
                ProductPricingHealthStatus.Ready,
                false,
                reviewDueDate);
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

        var approvedAt = approved.ApprovedAt ?? approved.UpdatedDate ?? approved.CreatedDate;
        return approvedAt.AddDays(options.ApprovedPricingReviewAfterDays);
    }
}

internal sealed record ProductPricingHealthResult(
    ProductPricingHealthStatus Status,
    bool RequiresPricingAction,
    DateTime? PricingReviewDueDate);
