using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Application.Features.Pricing.Authorization;

namespace HRM.Application.Tests.Features.CRM.Quotations;

public sealed class StandardPriceRealtimeComparisonCalculatorTests
{
    private static readonly DateTime CalculatedAt = new(2026, 9, 16, 10, 30, 0);

    [Fact]
    public void Calculate_MaterialCostIncreaseAdjustsApprovedPriceProportionally()
    {
        var result = StandardPriceRealtimeComparisonCalculator.Calculate(
            "VND",
            approvedStandardPrice: 120_000m,
            approvedMaterialCostSnapshot: 80_000m,
            realtimeMaterialCost: 88_000m,
            isMaterialCostComplete: true,
            warningThresholdPercent: 5m,
            calculatedAt: CalculatedAt);

        Assert.NotNull(result);
        Assert.Equal(132_000m, result.RealtimeAdjustedStandardPrice);
        Assert.Equal(12_000m, result.StandardPriceDifference);
        Assert.Equal(10m, result.StandardPriceDifferencePercent);
        Assert.Equal(8_000m, result.MaterialCostDifference);
        Assert.Equal(10m, result.MaterialCostDifferencePercent);
        Assert.Equal(MaterialCostMovementStatus.Increased, result.MovementStatus);
        Assert.True(result.IsIncreaseWarning);
    }

    [Fact]
    public void Calculate_MaterialCostDecreaseDoesNotRaiseIncreaseWarning()
    {
        var result = StandardPriceRealtimeComparisonCalculator.Calculate(
            "VND",
            approvedStandardPrice: 120_000m,
            approvedMaterialCostSnapshot: 80_000m,
            realtimeMaterialCost: 76_000m,
            isMaterialCostComplete: true,
            warningThresholdPercent: 5m,
            calculatedAt: CalculatedAt);

        Assert.NotNull(result);
        Assert.Equal(114_000m, result.RealtimeAdjustedStandardPrice);
        Assert.Equal(-6_000m, result.StandardPriceDifference);
        Assert.Equal(-5m, result.MaterialCostDifferencePercent);
        Assert.Equal(MaterialCostMovementStatus.Decreased, result.MovementStatus);
        Assert.False(result.IsIncreaseWarning);
    }

    [Fact]
    public void Calculate_IncompleteMaterialCostReturnsUnknownComparison()
    {
        var result = StandardPriceRealtimeComparisonCalculator.Calculate(
            "VND",
            approvedStandardPrice: 120_000m,
            approvedMaterialCostSnapshot: 80_000m,
            realtimeMaterialCost: null,
            isMaterialCostComplete: false,
            warningThresholdPercent: 5m,
            calculatedAt: CalculatedAt);

        Assert.NotNull(result);
        Assert.Null(result.RealtimeAdjustedStandardPrice);
        Assert.Null(result.StandardPriceDifference);
        Assert.Equal(MaterialCostMovementStatus.Unknown, result.MovementStatus);
        Assert.False(result.IsMaterialCostComplete);
        Assert.False(result.IsIncreaseWarning);
    }

    [Fact]
    public void Visibility_SaleKeepsAdjustedPriceButMasksAbsoluteMaterialCosts()
    {
        var comparison = StandardPriceRealtimeComparisonCalculator.Calculate(
            "VND",
            approvedStandardPrice: 120_000m,
            approvedMaterialCostSnapshot: 80_000m,
            realtimeMaterialCost: 88_000m,
            isMaterialCostComplete: true,
            warningThresholdPercent: 5m,
            calculatedAt: CalculatedAt);
        var saleAccess = new PricingAccessDecision(
            CanViewWorkbench: true,
            CanViewApprovedSellingPrice: true,
            CanViewSystemCalculatedPrice: false,
            CanViewMaterialCost: false,
            CanViewManufacturingCost: false,
            CanViewMargin: false,
            CanViewHistory: false,
            CanManage: false,
            CanApprove: false);

        var result = StandardPriceRealtimeComparisonVisibility.Apply(comparison, saleAccess);

        Assert.NotNull(result);
        Assert.Equal(132_000m, result.RealtimeAdjustedStandardPrice);
        Assert.Equal(10m, result.MaterialCostDifferencePercent);
        Assert.Null(result.ApprovedMaterialCostSnapshot);
        Assert.Null(result.RealtimeMaterialCost);
        Assert.Null(result.MaterialCostDifference);
    }
}
