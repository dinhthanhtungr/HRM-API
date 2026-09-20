using HRM.Application.Features.CRM.Quotations.Services;

namespace HRM.Application.Tests.Features.CRM.Quotations;

public sealed class ProductPricingRealtimeSourceQueryServiceTests
{
    [Fact]
    public void HasMaterialCostIncrease_ZeroThreshold_MatchesAnyPositiveIncrease()
    {
        Assert.True(ProductPricingRealtimeSourceQueryService.HasMaterialCostIncrease(100m, 100.0001m, 0m));
        Assert.False(ProductPricingRealtimeSourceQueryService.HasMaterialCostIncrease(100m, 100m, 0m));
        Assert.False(ProductPricingRealtimeSourceQueryService.HasMaterialCostIncrease(100m, 99m, 0m));
    }

    [Fact]
    public void HasMaterialCostIncrease_ConfiguredThreshold_RetainsWarningRule()
    {
        Assert.True(ProductPricingRealtimeSourceQueryService.HasMaterialCostIncrease(100m, 105m, 5m));
        Assert.False(ProductPricingRealtimeSourceQueryService.HasMaterialCostIncrease(100m, 104.999m, 5m));
    }
}
