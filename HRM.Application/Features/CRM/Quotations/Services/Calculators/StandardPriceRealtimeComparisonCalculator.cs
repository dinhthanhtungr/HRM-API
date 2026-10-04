using HRM.Application.Commons.Pricing.Helpers;
using HRM.Application.Features.CRM.Quotations.Dtos;

namespace HRM.Application.Features.CRM.Quotations.Services;

/// <summary>
/// Tính lại giá chuẩn tham chiếu từ NVL realtime, giữ nguyên chi phí sản xuất và
/// biên lợi nhuận trên giá bán đã được lưu tại thời điểm Approved.
/// </summary>
internal static class StandardPriceRealtimeComparisonCalculator
{
    public static StandardPriceRealtimeComparisonDto? Calculate(
        string currency,
        decimal? approvedStandardPrice,
        decimal? approvedMaterialCostSnapshot,
        decimal? approvedManufacturingCost,
        decimal? approvedProfitMarginRate,
        decimal? realtimeMaterialCost,
        bool isMaterialCostComplete,
        decimal warningThresholdPercent,
        DateTime calculatedAt)
    {
        if (approvedStandardPrice is not > 0m)
        {
            return null;
        }

        var normalizedThreshold = Math.Max(0m, warningThresholdPercent);
        if (approvedMaterialCostSnapshot is not > 0m ||
            approvedManufacturingCost is null or < 0m ||
            !isMaterialCostComplete ||
            !realtimeMaterialCost.HasValue)
        {
            return new StandardPriceRealtimeComparisonDto
            {
                Currency = currency,
                ApprovedStandardPrice = approvedStandardPrice.Value,
                ApprovedMaterialCostSnapshot = approvedMaterialCostSnapshot,
                RealtimeMaterialCost = realtimeMaterialCost,
                MovementStatus = MaterialCostMovementStatus.Unknown,
                IsMaterialCostComplete = false,
                WarningThresholdPercent = normalizedThreshold,
                CalculatedAt = calculatedAt
            };
        }

        var materialDifference = PricingRoundingRules.RoundStoredInput(
            realtimeMaterialCost.Value - approvedMaterialCostSnapshot.Value);
        var materialDifferencePercent = decimal.Round(
            materialDifference / approvedMaterialCostSnapshot.Value * 100m,
            4,
            MidpointRounding.AwayFromZero);
        var resolvedProfitMarginRate = StandardSellingPriceCalculator.ResolveProfitMarginRateOnSellingPrice(
            approvedProfitMarginRate,
            approvedStandardPrice,
            approvedMaterialCostSnapshot,
            approvedManufacturingCost);
        var realtimeCalculation = StandardSellingPriceCalculator.TryCalculateFromCostComponents(
            realtimeMaterialCost,
            approvedManufacturingCost,
            resolvedProfitMarginRate,
            currency);
        if (realtimeCalculation is null)
        {
            return new StandardPriceRealtimeComparisonDto
            {
                Currency = currency,
                ApprovedStandardPrice = approvedStandardPrice.Value,
                ApprovedMaterialCostSnapshot = approvedMaterialCostSnapshot,
                RealtimeMaterialCost = realtimeMaterialCost,
                MovementStatus = MaterialCostMovementStatus.Unknown,
                IsMaterialCostComplete = false,
                WarningThresholdPercent = normalizedThreshold,
                CalculatedAt = calculatedAt
            };
        }

        var adjustedStandardPrice = realtimeCalculation.StandardSellingPrice;
        var standardPriceDifference = PricingRoundingRules.RoundStoredInput(
            adjustedStandardPrice - approvedStandardPrice.Value);
        var standardPriceDifferencePercent = decimal.Round(
            standardPriceDifference / approvedStandardPrice.Value * 100m,
            4,
            MidpointRounding.AwayFromZero);
        var movementStatus = materialDifference switch
        {
            > 0m => MaterialCostMovementStatus.Increased,
            < 0m => MaterialCostMovementStatus.Decreased,
            _ => MaterialCostMovementStatus.Unchanged
        };

        return new StandardPriceRealtimeComparisonDto
        {
            Currency = currency,
            ApprovedStandardPrice = approvedStandardPrice.Value,
            RealtimeAdjustedStandardPrice = adjustedStandardPrice,
            StandardPriceDifference = standardPriceDifference,
            StandardPriceDifferencePercent = standardPriceDifferencePercent,
            RealtimeAdjustedPriceFormula = realtimeCalculation.DisplayFormula,
            ApprovedMaterialCostSnapshot = approvedMaterialCostSnapshot,
            RealtimeMaterialCost = realtimeMaterialCost,
            MaterialCostDifference = materialDifference,
            MaterialCostDifferencePercent = materialDifferencePercent,
            MovementStatus = movementStatus,
            IsMaterialCostComplete = true,
            IsIncreaseWarning = movementStatus == MaterialCostMovementStatus.Increased &&
                normalizedThreshold > 0m &&
                materialDifferencePercent >= normalizedThreshold,
            WarningThresholdPercent = normalizedThreshold,
            CalculatedAt = calculatedAt
        };
    }

}
