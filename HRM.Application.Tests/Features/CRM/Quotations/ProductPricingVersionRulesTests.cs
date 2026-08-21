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
            [new ProductPricingTierRequest
            {
                QuantityRangeLabel = "Contact for price",
                UnitPrice = 0m,
                SortOrder = 0
            }]);

        Assert.True(result.Success);
        Assert.Equal(0m, result.Data!.Single().UnitPrice);
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
        Assert.Equal([0, 1], result.Data!.Select(x => x.SortOrder));
        Assert.All(result.Data!, x => Assert.Equal(versionId, x.ProductPricingVersionId));
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
        var error = ProductPricingVersionRules.ValidatePricingValues(10m, 5m, 20m, 101m);

        Assert.NotNull(error);
    }

    [Fact]
    public void ResolveChangedField_UsesExplicitFieldBeforeValueComparison()
    {
        var result = ProductPricingVersionRules.ResolveChangedField(
            ProductPricingChangedField.ProfitMarginRate,
            10m,
            100m,
            10m,
            20m,
            200m,
            20m);

        Assert.Equal(ProductPricingChangedField.ProfitMarginRate, result);
    }
}
