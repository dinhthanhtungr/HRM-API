using HRM.Application.Features.Executive.ProductPricingReview.Services;
using HRM.Application.Features.Executive.ProductPricingReview.Dtos;

namespace HRM.Application.Tests.Features.Executive;

public sealed class ProductPricingReviewMaterialCategoryRulesTests
{
    [Theory]
    [InlineData("PIG", "Hàng hoá", PricingReviewMaterialCategoryGroup.Pigment, "Bột màu")]
    [InlineData(null, "Pigment", PricingReviewMaterialCategoryGroup.Pigment, "Bột màu")]
    [InlineData(null, "Bột màu", PricingReviewMaterialCategoryGroup.Pigment, "Bột màu")]
    [InlineData("ADD", "Hàng hoá", PricingReviewMaterialCategoryGroup.Additive, "Phụ gia")]
    [InlineData(null, "Phu gia", PricingReviewMaterialCategoryGroup.Additive, "Phụ gia")]
    [InlineData("VRG", "Hàng hoá", PricingReviewMaterialCategoryGroup.Resin, "Nhựa")]
    [InlineData(null, "Hạt nhựa", PricingReviewMaterialCategoryGroup.Resin, "Nhựa")]
    [InlineData(null, "Polymer", PricingReviewMaterialCategoryGroup.Resin, "Nhựa")]
    [InlineData(null, "KH", PricingReviewMaterialCategoryGroup.Other, "Khác")]
    [InlineData(null, "Hàng hoá", PricingReviewMaterialCategoryGroup.Other, "Khác")]
    [InlineData(null, null, PricingReviewMaterialCategoryGroup.Other, "Khác")]
    public void Resolve_returns_one_of_four_canonical_groups(
        string? categoryCode,
        string? categoryName,
        PricingReviewMaterialCategoryGroup expectedGroup,
        string expectedDisplayName)
    {
        var group = ProductPricingReviewMaterialCategoryRules.Resolve(categoryCode, categoryName);

        Assert.Equal(expectedGroup, group);
        Assert.Equal(expectedDisplayName, ProductPricingReviewMaterialCategoryRules.GetDisplayName(group));
    }
}
