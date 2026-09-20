using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Domain.Enums.Formulas;
using HRM.Domain.Enums.Materials;

namespace HRM.Application.Tests.Features.CRM.Quotations;

public sealed class ProductPricingMaterialAvailabilityRulesTests
{
    [Theory]
    [InlineData(ItemType.Material)]
    [InlineData(ItemType.MaterialFailure)]
    public void Resolve_MaterialTypes_ReturnCanonicalAvailability(ItemType itemType)
    {
        var expectedAvailableDate = new DateTime(2026, 10, 1);

        var result = ProductPricingMaterialAvailabilityRules.Resolve(
            itemType,
            Guid.NewGuid(),
            MaterialPurchaseStatus.Unavailable,
            "Ngừng mua tạm thời",
            new DateTime(2026, 9, 18),
            expectedAvailableDate);

        Assert.NotNull(result);
        Assert.Equal(MaterialPurchaseStatus.Unavailable, result.Status);
        Assert.False(result.IsPurchaseAvailable);
        Assert.Equal(expectedAvailableDate, result.ExpectedAvailableDate);
    }

    [Theory]
    [InlineData(ItemType.Product)]
    [InlineData(ItemType.ProductFailure)]
    public void Resolve_ProductTypes_ReturnNull(ItemType itemType)
    {
        var result = ProductPricingMaterialAvailabilityRules.Resolve(
            itemType,
            Guid.NewGuid(),
            MaterialPurchaseStatus.Unavailable,
            "Không áp dụng cho thành phẩm",
            null,
            null);

        Assert.Null(result);
    }

    [Fact]
    public void Resolve_MaterialWithoutExplicitStatus_DefaultsToAvailable()
    {
        var result = ProductPricingMaterialAvailabilityRules.Resolve(
            ItemType.Material,
            Guid.NewGuid(),
            null,
            null,
            null,
            null);

        Assert.NotNull(result);
        Assert.Equal(MaterialPurchaseStatus.Available, result.Status);
        Assert.True(result.IsPurchaseAvailable);
    }
}
