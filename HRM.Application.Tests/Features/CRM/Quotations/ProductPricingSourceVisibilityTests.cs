using HRM.Application.Features.Pricing.Authorization;
using HRM.Application.Commons.Pricing.Dtos;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;

namespace HRM.Application.Tests.Features.CRM.Quotations;

public sealed class ProductPricingSourceVisibilityTests
{
    [Fact]
    public void Apply_SaleCannotSeeSystemCalculatedPriceOrInternalCosts()
    {
        var source = FullSource();

        var result = ProductPricingSourceVisibility.Apply(source, SaleAccess());

        Assert.Equal(source.SourceId, result.SourceId);
        Assert.Equal(source.ExternalId, result.ExternalId);
        Assert.Null(result.StandardSellingPrice);
        Assert.Empty(result.PriceTierTemplates);
        Assert.Null(result.CurrentMaterialCost);
        Assert.Null(result.ManufacturingCost);
        Assert.Null(result.ProfitMarginRate);
        Assert.Null(result.Pricing);
        Assert.Empty(result.Materials);
    }

    [Fact]
    public void Apply_ManagerKeepsFullPricingSource()
    {
        var source = FullSource();

        var result = ProductPricingSourceVisibility.Apply(source, ManagerAccess());

        Assert.Equal(source.StandardSellingPrice, result.StandardSellingPrice);
        Assert.Equal(source.CurrentMaterialCost, result.CurrentMaterialCost);
        Assert.Equal(source.ManufacturingCost, result.ManufacturingCost);
        Assert.Equal(source.ProfitMarginRate, result.ProfitMarginRate);
        Assert.Same(source.Pricing, result.Pricing);
        Assert.Single(result.PriceTierTemplates);
        Assert.Single(result.Materials);
    }

    private static ProductPricingSourceOptionDto FullSource()
        => new()
        {
            SourceId = Guid.NewGuid(),
            ExternalId = "VU260800009",
            Name = "F001",
            CurrentMaterialCost = 20_000m,
            ManufacturingCost = 5_000m,
            StandardSellingPrice = 35_000m,
            ProfitMarginRate = 28.5714m,
            IsCurrentMaterialCostComplete = true,
            Pricing = new FormulaPriceCalculationDto(),
            PriceTierTemplates = [new FormulaSuggestedPriceTierDto()],
            Materials = [new QuotationProductPricingMaterialDto()]
        };

    private static PricingAccessDecision SaleAccess()
        => new(
            CanViewWorkbench: true,
            CanViewApprovedSellingPrice: true,
            CanViewSystemCalculatedPrice: false,
            CanViewMaterialCost: false,
            CanViewManufacturingCost: false,
            CanViewMargin: false,
            CanViewHistory: false,
            CanManage: false,
            CanApprove: false);

    private static PricingAccessDecision ManagerAccess()
        => new(
            CanViewWorkbench: true,
            CanViewApprovedSellingPrice: true,
            CanViewSystemCalculatedPrice: true,
            CanViewMaterialCost: true,
            CanViewManufacturingCost: true,
            CanViewMargin: true,
            CanViewHistory: true,
            CanManage: true,
            CanApprove: true);
}
