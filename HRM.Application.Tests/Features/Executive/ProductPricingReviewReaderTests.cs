using HRM.Application.Commons.Pricing.Dtos;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.Executive.ProductPricingReview.Dtos;
using HRM.Application.Features.Executive.ProductPricingReview.Services;
using HRM.Domain.Entities.CustomerSchema;

namespace HRM.Application.Tests.Features.Executive;

public sealed class ProductPricingReviewReaderTests
{
    [Fact]
    public void ReviewDto_ExposesSharedRealtimePriceComparison()
    {
        var comparison = new StandardPriceRealtimeComparisonDto
        {
            Currency = "VND",
            ApprovedStandardPrice = 244_514m,
            RealtimeAdjustedStandardPrice = 244_514m,
            MovementStatus = MaterialCostMovementStatus.Unchanged,
            IsMaterialCostComplete = true,
            WarningThresholdPercent = 5m
        };

        var result = new ProductPricingReviewDto
        {
            RealtimePriceComparison = comparison
        };

        Assert.Same(comparison, result.RealtimePriceComparison);
    }

    [Fact]
    public void MapVersion_ReturnsPublisherNote()
    {
        var result = ProductPricingReviewReader.MapVersion(new ProductPricingVersion
        {
            ProductPricingVersionId = Guid.NewGuid(),
            Currency = "VND",
            PublisherNote = "Áp dụng cho đơn từ 100 kg"
        });

        Assert.Equal("Áp dụng cho đơn từ 100 kg", result.PublisherNote);
    }

    [Fact]
    public void MapVersion_ForwardsRealtimePriceComparison()
    {
        var comparison = new StandardPriceRealtimeComparisonDto
        {
            Currency = "VND",
            ApprovedStandardPrice = 244_514m,
            RealtimeAdjustedStandardPrice = 244_514m,
            MovementStatus = MaterialCostMovementStatus.Unchanged,
            IsMaterialCostComplete = true,
            WarningThresholdPercent = 5m,
            CalculatedAt = new DateTime(2026, 9, 17, 11, 59, 14)
        };

        var result = ProductPricingReviewReader.MapVersion(
            new ProductPricingVersion
            {
                ProductPricingVersionId = Guid.NewGuid(),
                Currency = "VND"
            },
            comparison);

        Assert.Same(comparison, result.RealtimePriceComparison);
    }

    [Fact]
    public void ResolveEditorPriceTiers_UsesCurrentPolicyTiers()
    {
        var result = ProductPricingReviewReader.ResolveEditorPriceTiers(
            [new FormulaSuggestedPriceTierDto
            {
                QuantityRangeLabel = "Policy",
                UnitPrice = 100_000m,
                SortOrder = 0
            }]);

        var tier = Assert.Single(result);
        Assert.Null(tier.PricingTierId);
        Assert.Equal("Policy", tier.QuantityRangeLabel);
        Assert.False(tier.IsStored);
        Assert.False(tier.RequiresManualPrice);
    }

    [Fact]
    public void ResolveEditorPriceTiers_PreservesPolicyManualPriceRequirement()
    {
        var result = ProductPricingReviewReader.ResolveEditorPriceTiers(
            [new FormulaSuggestedPriceTierDto
            {
                QuantityRangeLabel = "Custom",
                MinQuantity = 100m,
                UnitPrice = null,
                RequiresManualPrice = true,
                SortOrder = 2
            }]);

        var tier = Assert.Single(result);
        Assert.Null(tier.PricingTierId);
        Assert.Null(tier.UnitPrice);
        Assert.False(tier.IsStored);
        Assert.True(tier.RequiresManualPrice);
    }
}
