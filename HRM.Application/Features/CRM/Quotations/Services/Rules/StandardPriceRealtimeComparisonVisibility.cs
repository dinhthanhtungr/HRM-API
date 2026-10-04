using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.Pricing.Authorization;

namespace HRM.Application.Features.CRM.Quotations.Services;

internal static class StandardPriceRealtimeComparisonVisibility
{
    public static StandardPriceRealtimeComparisonDto? Apply(
        StandardPriceRealtimeComparisonDto? source,
        PricingAccessDecision access)
    {
        if (source is null || !access.CanViewApprovedSellingPrice)
        {
            return null;
        }

        return new StandardPriceRealtimeComparisonDto
        {
            Currency = source.Currency,
            ApprovedStandardPrice = source.ApprovedStandardPrice,
            RealtimeAdjustedStandardPrice = source.RealtimeAdjustedStandardPrice,
            StandardPriceDifference = source.StandardPriceDifference,
            StandardPriceDifferencePercent = source.StandardPriceDifferencePercent,
            RealtimeAdjustedPriceFormula = access.CanViewMaterialCost &&
                access.CanViewManufacturingCost &&
                access.CanViewMargin
                ? source.RealtimeAdjustedPriceFormula
                : null,
            ApprovedMaterialCostSnapshot = access.CanViewMaterialCost
                ? source.ApprovedMaterialCostSnapshot
                : null,
            RealtimeMaterialCost = access.CanViewMaterialCost
                ? source.RealtimeMaterialCost
                : null,
            MaterialCostDifference = access.CanViewMaterialCost
                ? source.MaterialCostDifference
                : null,
            MaterialCostDifferencePercent = source.MaterialCostDifferencePercent,
            MovementStatus = source.MovementStatus,
            IsMaterialCostComplete = source.IsMaterialCostComplete,
            IsIncreaseWarning = source.IsIncreaseWarning,
            WarningThresholdPercent = source.WarningThresholdPercent,
            CalculatedAt = source.CalculatedAt
        };
    }
}
