using HRM.Application.Commons.Pricing.Dtos;
using HRM.Application.Commons.Pricing.Helpers;
using HRM.Application.Commons.Pricing.Models;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.Formulas;

namespace HRM.Application.Tests.Features.CRM.Quotations;

public sealed class PricingRoundingRulesTests
{
    [Fact]
    public void RealtimeMaterialCost_RoundsSystemTotalToInteger()
    {
        var itemId = Guid.NewGuid();
        var result = FormulaRealtimeMaterialCostCalculator.Calculate(
            [new FormulaMaterialCostItem(itemId, ItemType.Material, 1m)],
            new Dictionary<PriceItemKey, LatestItemPriceDto>
            {
                [new PriceItemKey(ItemType.Material, itemId)] = new()
                {
                    ItemType = ItemType.Material,
                    ItemId = itemId,
                    CurrentPrice = 100.5m,
                    PriceSource = LatestPriceSourceType.PurchaseOrder
                }
            });

        Assert.True(result.IsComplete);
        Assert.Equal(101m, result.MaterialCost);
    }

    [Fact]
    public void FormulaPricing_RoundsCalculatedPricesButPreservesStoredSellingPrice()
    {
        var calculated = FormulaPriceCalculator.Calculate(
            PowderPolicy(),
            materialCost: 100.6m,
            manufacturingCost: 10.25m,
            standardSellingPrice: null);
        var stored = FormulaPriceCalculator.Calculate(
            PowderPolicy(),
            materialCost: 100.6m,
            manufacturingCost: 10.25m,
            standardSellingPrice: 123.456789m);

        Assert.Equal(101m, calculated.MaterialCost);
        Assert.Equal(111m, calculated.CostBase);
        Assert.Equal(111m, calculated.StandardSellingPrice);
        Assert.All(
            calculated.SuggestedPriceTiers.Where(x => x.UnitPrice.HasValue),
            tier => Assert.Equal(0m, tier.UnitPrice!.Value % 1m));
        Assert.Equal(123.456789m, stored.StandardSellingPrice);
    }

    [Fact]
    public void FormulaPricing_UsesConfiguredCompoundProfileAndTierTemplates()
    {
        var policy = new FormulaPricingPolicyDefinition(
            FormulaPricingProfile.Compound,
            0m,
            0m,
            FormulaPricingRoundingRule.Nearest,
            1m,
            [
                new("< 100 kg", null, 100m, true, false, 0m, 0),
                new("> 10 tấn", 10000m, null, false, true, 0m, 1)
            ]);
        var templates = FormulaPriceCalculator.BuildPriceTierTemplates(policy);

        Assert.Equal(2, templates.Count);
        Assert.Equal("< 100 kg", templates[0].QuantityRangeLabel);
        Assert.Equal("> 10 tấn", templates[^1].QuantityRangeLabel);
        Assert.All(templates, tier => Assert.Null(tier.UnitPrice));
        Assert.All(templates, tier => Assert.False(tier.RequiresManualPrice));
    }

    private static FormulaPricingPolicyDefinition PowderPolicy()
        => new(
            FormulaPricingProfile.Powder,
            10m,
            0m,
            FormulaPricingRoundingRule.Nearest,
            1m,
            [new("All", null, null, true, true, 0m, 0)]);

    [Fact]
    public void ProductPricingTier_PreservesUserEnteredDecimals()
    {
        var result = ProductPricingVersionRules.BuildTiers(
            Guid.NewGuid(),
            [
                new ProductPricingTierRequest
                {
                    QuantityRangeLabel = "Manual",
                    UnitPrice = 12.345678m,
                    SortOrder = 0
                }
            ]);

        Assert.True(result.Success);
        Assert.Equal(12.345678m, result.Data!.Single().UnitPrice);
    }
}
