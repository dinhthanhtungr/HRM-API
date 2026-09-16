using HRM.Application.Commons.Pricing.Rules;

namespace HRM.Application.Tests.Commons.Pricing;

public sealed class InternalMaterialCostingRulesTests
{
    [Fact]
    public void GroundResin_AddsGrindingCostAndNormalizesVietnameseName()
    {
        var matched = InternalMaterialCostingRules.TryResolveMaterialRule(
            "Hạt PP trắng nghiền", out var rule, out var sourceNameKey);

        Assert.True(matched);
        Assert.Equal(InternalMaterialCostingRule.GroundResin, rule);
        Assert.Equal("PP TRANG", sourceNameKey);
        Assert.Equal(40_000m, InternalMaterialCostingRules.ApplyMaterialRule(rule, 35_000m));
    }

    [Fact]
    public void DilutedPigment_UsesSeventyPercentOfItsSourcePrice()
    {
        var matched = InternalMaterialCostingRules.TryResolveMaterialRule(
            "Bột màu trắng pha loãng 10%", out var rule, out var sourceNameKey);

        Assert.True(matched);
        Assert.Equal(InternalMaterialCostingRule.DilutedPigment, rule);
        Assert.Equal("MAU TRANG", sourceNameKey);
        Assert.Equal(210_000m, InternalMaterialCostingRules.ApplyMaterialRule(rule, 300_000m));
    }

    [Theory]
    [InlineData("CMB", 15_000)]
    [InlineData("CMP", 10_000)]
    [InlineData("PIG", 0)]
    public void ProductCategories_ApplyOnlyTheApprovedManufacturingSurcharge(
        string categoryCode,
        decimal expectedSurcharge)
    {
        Assert.Equal(expectedSurcharge,
            InternalMaterialCostingRules.GetProductManufacturingSurcharge(categoryCode));
    }

    [Fact]
    public void ProductNameRule_TakesPrecedenceOverCompoundCategory()
    {
        var adjustment = InternalMaterialCostingRules.ResolveProductCostAdjustment(
            "Compound PP trắng nghiền", "CMP");

        Assert.Equal(InternalMaterialCostingRule.GroundResin, adjustment.MaterialRule);
        Assert.Equal(0m, adjustment.SurchargePerKg);
        Assert.Equal(40_000m, InternalMaterialCostingRules.ApplyProductCostAdjustment(35_000m, adjustment));
    }
}
