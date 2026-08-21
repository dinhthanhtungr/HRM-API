using HRM.Application.Commons.Pricing.Helpers;
using HRM.Application.Commons.Pricing.Models;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Queries.GetProductPricingWorkbench;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Tests.Features.CRM.Quotations;

public sealed class ProductPricingWorkbenchMapperTests
{
    [Fact]
    public void MapSummary_UsesRealtimeMaterialCostAndReportsSnapshotDifference()
    {
        var productId = Guid.NewGuid();
        var product = new ProductRow
        {
            ProductId = productId,
            ProductCode = "TP4909",
            ProductName = "Test product"
        };
        var draft = new PricingVersionRow
        {
            ProductPricingVersionId = Guid.NewGuid(),
            ProductId = productId,
            SourceType = ProductPricingSourceType.Formula,
            SourceId = Guid.NewGuid(),
            MaterialCostSnapshot = 100m,
            ManufacturingCost = 10m,
            StandardSellingPrice = 143m,
            Status = ProductPricingStatus.Draft,
            Version = 1,
            CreatedDate = new DateTime(2026, 8, 18)
        };
        var pricing = FormulaPriceCalculator.Calculate(
            new FormulaPricingPolicyDefinition(
                HRM.Domain.Enums.Formulas.FormulaPricingProfile.Powder,
                10m,
                10m,
                FormulaPricingRoundingRule.Nearest,
                1m,
                []),
            120m,
            10m,
            null);
        var source = new ProductPricingSourceOptionDto
        {
            SourceType = ProductPricingSourceType.Formula,
            SourceId = draft.SourceId!.Value,
            IsEligible = true,
            CurrentMaterialCost = 120m,
            IsCurrentMaterialCostComplete = true,
            ManufacturingCost = 10m,
            Pricing = pricing
        };
        var quotationId = Guid.NewGuid();
        var requests = new ProductPricingRequestRow[]
        {
            new()
            {
                ProductId = productId,
                QuotationId = quotationId,
                RequestedAt = new DateTime(2026, 8, 18)
            },
            new()
            {
                ProductId = productId,
                QuotationId = quotationId,
                RequestedAt = new DateTime(2026, 8, 18)
            }
        };

        var result = ProductPricingWorkbenchMapper.MapSummary(
            product,
            "VND",
            draft,
            approved: null,
            source,
            requests);

        Assert.Equal(120m, result.CurrentMaterialCost);
        Assert.Equal(100m, result.StoredMaterialCostSnapshot);
        Assert.Equal(20m, result.MaterialCostDifference);
        Assert.Equal(20m, result.MaterialCostDifferencePercent);
        Assert.Equal(143m, result.StandardSellingPrice);
        Assert.Equal(10m, result.ProfitMarginRate);
        Assert.Equal(1, result.WaitingQuotationCount);
        Assert.Equal(ProductPricingLookupStatus.Draft, result.PricingStatus);
        Assert.Same(pricing, ProductPricingWorkbenchMapper.BuildEffectivePricing(draft, source));
    }

    [Fact]
    public void MapSummary_ApprovedPricingClearsWaitingCount()
    {
        var productId = Guid.NewGuid();
        var approved = new PricingVersionRow
        {
            ProductPricingVersionId = Guid.NewGuid(),
            ProductId = productId,
            Status = ProductPricingStatus.Approved,
            Version = 2,
            CreatedDate = new DateTime(2026, 8, 18)
        };

        var result = ProductPricingWorkbenchMapper.MapSummary(
            new ProductRow { ProductId = productId },
            "VND",
            draft: null,
            approved,
            source: null,
            [
                new ProductPricingRequestRow
                {
                    ProductId = productId,
                    QuotationId = Guid.NewGuid(),
                    RequestedAt = new DateTime(2026, 8, 18)
                }
            ]);

        Assert.Equal(0, result.WaitingQuotationCount);
        Assert.Null(result.LatestRequestedAt);
        Assert.Equal(ProductPricingLookupStatus.Approved, result.PricingStatus);
    }

    [Fact]
    public void MapSummary_SystemSuggestionIsAnUnpersistedDraft()
    {
        var productId = Guid.NewGuid();
        var source = new ProductPricingSourceOptionDto
        {
            SourceType = ProductPricingSourceType.Formula,
            SourceId = Guid.NewGuid(),
            IsEligible = true
        };

        var result = ProductPricingWorkbenchMapper.MapSummary(
            new ProductRow { ProductId = productId },
            "VND",
            draft: null,
            approved: null,
            source,
            requests: []);

        Assert.Equal(ProductPricingLookupStatus.Draft, result.PricingStatus);
        Assert.True(result.IsSystemCalculatedDraft);
        Assert.Null(result.DraftPricingVersionId);
        Assert.Null(result.ApprovedPricingVersionId);
    }

    [Fact]
    public void MapSummary_ReportsPricingPolicyMissingWithoutSnapshotFallback()
    {
        var result = ProductPricingWorkbenchMapper.MapSummary(
            new ProductRow { ProductId = Guid.NewGuid() },
            "USD",
            draft: null,
            approved: null,
            new ProductPricingSourceOptionDto
            {
                SourceType = ProductPricingSourceType.Formula,
                SourceId = Guid.NewGuid(),
                PricingStatus = FormulaPricingPolicyRules.PricingPolicyMissing,
                Pricing = null
            },
            requests: []);

        Assert.Equal(ProductPricingLookupStatus.PricingPolicyMissing, result.PricingStatus);
        Assert.Null(ProductPricingWorkbenchMapper.BuildEffectivePricing(null, new ProductPricingSourceOptionDto
        {
            PricingStatus = FormulaPricingPolicyRules.PricingPolicyMissing
        }));
    }
}
