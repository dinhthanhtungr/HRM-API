using HRM.Application.Commons.Pricing.Dtos;
using HRM.Application.Features.Executive.ProductPricingReview.Services;
using HRM.Domain.Entities.CustomerSchema;

namespace HRM.Application.Tests.Features.Executive;

public sealed class ProductPricingReviewReaderTests
{
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
