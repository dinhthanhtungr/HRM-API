using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Tests.Features.CRM.Quotations;

public sealed class ProductPricingVersionRulesTests
{
    [Fact]
    public void BuildTiers_AllowsEmptyDraftPricing()
    {
        var result = ProductPricingVersionRules.BuildTiers(Guid.NewGuid(), []);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data);
    }

    [Fact]
    public void BuildTiers_AllowsZeroUnitPrice()
    {
        var result = ProductPricingVersionRules.BuildTiers(
            Guid.NewGuid(),
            [
                new ProductPricingTierRequest
                {
                    QuantityRangeLabel = "Contact for price",
                    UnitPrice = 0m,
                    SortOrder = 0
                }
            ]);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(0m, result.Data.Single().UnitPrice);
    }

    [Fact]
    public void BuildTiers_CreatesOrderedNonOverlappingTiers()
    {
        var versionId = Guid.NewGuid();
        var result = ProductPricingVersionRules.BuildTiers(versionId,
        [
            new ProductPricingTierRequest
            {
                QuantityRangeLabel = "50-100",
                MinQuantity = 50m,
                MaxQuantity = 100m,
                UnitPrice = 90_000m,
                SortOrder = 1
            },
            new ProductPricingTierRequest
            {
                QuantityRangeLabel = "< 50",
                MaxQuantity = 50m,
                MaxInclusive = false,
                UnitPrice = 100_000m,
                SortOrder = 0
            }
        ]);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal([0, 1], result.Data.Select(x => x.SortOrder));
        Assert.All(result.Data, x => Assert.Equal(versionId, x.ProductPricingVersionId));
    }

    [Fact]
    public void BuildTiers_RejectsOverlappingRanges()
    {
        var result = ProductPricingVersionRules.BuildTiers(Guid.NewGuid(),
        [
            new ProductPricingTierRequest
            {
                QuantityRangeLabel = "0-100",
                MinQuantity = 0m,
                MaxQuantity = 100m,
                UnitPrice = 100m,
                SortOrder = 0
            },
            new ProductPricingTierRequest
            {
                QuantityRangeLabel = "100-200",
                MinQuantity = 100m,
                MaxQuantity = 200m,
                UnitPrice = 90m,
                SortOrder = 1
            }
        ]);

        Assert.False(result.Success);
        Assert.Contains("overlapping", result.Message);
    }

    [Fact]
    public void ValidatePricingValues_RejectsMarginAboveOneHundredPercent()
    {
        var error = ProductPricingVersionRules.ValidatePricingValues(
            materialCost: 10m,
            manufacturingCost: 5m,
            standardSellingPrice: 20m,
            profitMarginRate: 101m);

        Assert.NotNull(error);
    }

    [Fact]
    public void NormalizePricingValues_ManufacturingCostChange_PreservesMarginAndRecalculatesPrice()
    {
        var result = ProductPricingVersionRules.NormalizePricingValues(
            materialCost: 100m,
            manufacturingCost: 20m,
            standardSellingPrice: 999m,
            profitMarginRate: 25m,
            ProductPricingChangedField.ManufacturingCost);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(100m, result.Data.MaterialCostSnapshot);
        Assert.Equal(20m, result.Data.ManufacturingCost);
        Assert.Equal(150m, result.Data.StandardSellingPrice);
        Assert.Equal(25m, result.Data.ProfitMarginRate);
    }

    [Fact]
    public void NormalizePricingValues_StandardSellingPriceChange_RecalculatesMargin()
    {
        var result = ProductPricingVersionRules.NormalizePricingValues(
            materialCost: 100m,
            manufacturingCost: 20m,
            standardSellingPrice: 150m,
            profitMarginRate: 99m,
            ProductPricingChangedField.StandardSellingPrice);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(150m, result.Data.StandardSellingPrice);
        Assert.Equal(25m, result.Data.ProfitMarginRate);
    }

    [Fact]
    public void NormalizePricingValues_ProfitMarginRateChange_RecalculatesPrice()
    {
        var result = ProductPricingVersionRules.NormalizePricingValues(
            materialCost: 100m,
            manufacturingCost: 20m,
            standardSellingPrice: 999m,
            profitMarginRate: 25m,
            ProductPricingChangedField.ProfitMarginRate);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(150m, result.Data.StandardSellingPrice);
        Assert.Equal(25m, result.Data.ProfitMarginRate);
    }

    [Fact]
    public void NormalizePricingValues_NoInputPrice_DefaultsToCostBase()
    {
        var result = ProductPricingVersionRules.NormalizePricingValues(
            materialCost: 100m,
            manufacturingCost: 20m,
            standardSellingPrice: null,
            profitMarginRate: null,
            changedField: null);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(120m, result.Data.StandardSellingPrice);
        Assert.Equal(0m, result.Data.ProfitMarginRate);
    }

    [Fact]
    public void NormalizePricingValues_ExplicitChangeWithoutRealtimeMaterialCost_Fails()
    {
        var result = ProductPricingVersionRules.NormalizePricingValues(
            materialCost: null,
            manufacturingCost: 20m,
            standardSellingPrice: 150m,
            profitMarginRate: null,
            ProductPricingChangedField.StandardSellingPrice);

        Assert.False(result.Success);
        Assert.Contains("Realtime material cost", result.Message);
    }

    [Fact]
    public void ResolveChangedField_UsesExplicitFieldBeforeValueComparison()
    {
        var result = ProductPricingVersionRules.ResolveChangedField(
            ProductPricingChangedField.ProfitMarginRate,
            currentManufacturingCost: 10m,
            currentStandardSellingPrice: 100m,
            currentProfitMarginRate: 10m,
            requestedManufacturingCost: 20m,
            requestedStandardSellingPrice: 200m,
            requestedProfitMarginRate: 20m);

        Assert.Equal(ProductPricingChangedField.ProfitMarginRate, result);
    }
}
