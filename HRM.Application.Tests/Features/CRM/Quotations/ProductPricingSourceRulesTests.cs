using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.Manufacturings;
using HRM.Domain.Enums.Products;

namespace HRM.Application.Tests.Features.CRM.Quotations;

public sealed class ProductPricingSourceRulesTests
{
    [Theory]
    [InlineData(FormulaStatus.Approved)]
    [InlineData(FormulaStatus.SampleSent)]
    [InlineData(FormulaStatus.Completed)]
    public void EligibleFormulaStatuses_IncludeLabApprovedWorkflowStates(
        FormulaStatus status)
    {
        Assert.Contains(status.ToString(), ProductPricingSourceRules.EligibleFormulaStatuses);
    }

    [Theory]
    [InlineData(FormulaStatus.Draft)]
    [InlineData(FormulaStatus.PendingSaleConfirmation)]
    [InlineData(FormulaStatus.Rejected)]
    [InlineData(FormulaStatus.Cancelled)]
    public void EligibleFormulaStatuses_ExcludeUnconfirmedOrTerminalStates(
        FormulaStatus status)
    {
        Assert.DoesNotContain(status.ToString(), ProductPricingSourceRules.EligibleFormulaStatuses);
    }

    [Theory]
    [InlineData(ManufacturingProductOrderFormula.IsSelect)]
    [InlineData(ManufacturingProductOrderFormula.Processing)]
    [InlineData(ManufacturingProductOrderFormula.Completed)]
    public void EligibleManufacturingStatuses_IncludeSelectedProductionStates(
        ManufacturingProductOrderFormula status)
    {
        Assert.Contains(
            status.ToString(),
            ProductPricingSourceRules.EligibleManufacturingFormulaStatuses);
    }

    [Fact]
    public void Mapper_HidesSensitivePricingButKeepsSellingPriceAndTiers()
    {
        var entity = new ProductPricingVersion
        {
            ProductPricingVersionId = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            CompanyId = Guid.NewGuid(),
            SourceManufacturingFormulaId = Guid.NewGuid(),
            Product = new Product { ColourCode = "TP4909", Name = "Product" },
            MaterialCostSnapshot = 10m,
            ManufacturingCost = 20m,
            StandardSellingPrice = 50m,
            ProfitMarginRate = 40m,
            PriceTiers =
            [
                new ProductPricingTier
                {
                    ProductPricingTierId = Guid.NewGuid(),
                    UnitPrice = 50m
                }
            ]
        };

        var dto = ProductPricingVersionMapper.ToDto(
            entity,
            includeSensitivePricing: false);

        Assert.Equal(ProductPricingSourceType.ManufacturingFormula, dto.SourceType);
        Assert.Equal(entity.SourceManufacturingFormulaId, dto.SourceId);
        Assert.Null(dto.MaterialCostSnapshot);
        Assert.Null(dto.ManufacturingCost);
        Assert.Null(dto.ProfitMarginRate);
        Assert.Equal(50m, dto.StandardSellingPrice);
        Assert.Single(dto.PriceTiers);
    }
}
