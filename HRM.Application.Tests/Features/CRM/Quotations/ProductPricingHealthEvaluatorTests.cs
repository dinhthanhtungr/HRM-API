using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Tests.Features.CRM.Quotations;

public sealed class ProductPricingHealthEvaluatorTests
{
    private static readonly DateTime Now = new(2026, 8, 26, 9, 0, 0);

    [Fact]
    public void Evaluate_MissingMaterialPriceIsWarningOnlyAndKeepsReviewDueDate()
    {
        var result = ProductPricingHealthEvaluator.Evaluate(
            new ProductPricingSourceOptionDto
            {
                IsEligible = true,
                IsCurrentMaterialCostComplete = false,
                MissingMaterialPriceCount = 1
            },
            draft: null,
            approved: ApprovedPricing(Now.AddDays(-45)),
            now: Now,
            options: new QuotationFeatureOptions
            {
                ApprovedPricingReviewAfterDays = 30
            });

        Assert.Equal(ProductPricingHealthStatus.MissingMaterialPrice, result.Status);
        Assert.False(result.RequiresPricingAction);
        Assert.Equal(Now.AddDays(-15), result.PricingReviewDueDate);
    }

    [Fact]
    public void Evaluate_MaterialCostIncreaseAtThresholdRequiresPricingAction()
    {
        var result = ProductPricingHealthEvaluator.Evaluate(
            ReadySource(currentMaterialCost: 105m),
            draft: null,
            approved: ApprovedPricing(
                Now.AddDays(-1),
                materialCostSnapshot: 100m,
                standardSellingPrice: 100m),
            now: Now,
            options: new QuotationFeatureOptions { MaterialCostChangeThresholdPercent = 5m });

        Assert.Equal(ProductPricingHealthStatus.MaterialCostChanged, result.Status);
        Assert.True(result.RequiresPricingAction);
    }

    [Fact]
    public void Evaluate_MaterialCostIncreaseBelowThresholdDoesNotRequirePricingAction()
    {
        var result = ProductPricingHealthEvaluator.Evaluate(
            ReadySource(currentMaterialCost: 104.99m),
            draft: null,
            approved: ApprovedPricing(
                Now.AddDays(-1),
                materialCostSnapshot: 100m,
                standardSellingPrice: 100m),
            now: Now,
            options: new QuotationFeatureOptions { MaterialCostChangeThresholdPercent = 5m });

        Assert.Equal(ProductPricingHealthStatus.Ready, result.Status);
        Assert.False(result.RequiresPricingAction);
    }

    [Fact]
    public void Evaluate_ExpiredApprovedPricingRequiresExplicitReapproval()
    {
        var result = ProductPricingHealthEvaluator.Evaluate(
            ReadySource(),
            draft: null,
            approved: ApprovedPricing(Now.AddDays(-31)),
            now: Now,
            options: new QuotationFeatureOptions { ApprovedPricingReviewAfterDays = 30 });

        Assert.Equal(ProductPricingHealthStatus.PendingReapproval, result.Status);
        Assert.Equal(ProductStandardPriceState.PendingReapproval, result.StandardPriceState);
        Assert.True(result.IsReviewExpired);
        Assert.Equal(Now.AddDays(-1), result.PricingReviewDueDate);
    }

    [Fact]
    public void Evaluate_FormulaConfirmedAfterApprovedPriceRequiresReapproval()
    {
        var result = ProductPricingHealthEvaluator.Evaluate(
            ReadySource(),
            draft: null,
            approved: ApprovedPricing(Now.AddDays(-2)),
            now: Now,
            options: new QuotationFeatureOptions(),
            latestFormulaConfirmedAt: Now.AddDays(-1));

        Assert.Equal(ProductPricingHealthStatus.PendingReapproval, result.Status);
        Assert.Equal(ProductStandardPriceState.PendingReapproval, result.StandardPriceState);
        Assert.True(result.HasFormulaConfirmationPending);
    }

    [Fact]
    public void Evaluate_InvalidSourceKeepsExistingApprovedStandardPrice()
    {
        var result = ProductPricingHealthEvaluator.Evaluate(
            new ProductPricingSourceOptionDto { IsEligible = false },
            draft: null,
            approved: ApprovedPricing(Now.AddDays(-1)),
            now: Now,
            options: new QuotationFeatureOptions());

        Assert.Equal(ProductPricingHealthStatus.SourceNoLongerEligible, result.Status);
        Assert.True(result.RequiresPricingAction);
        Assert.Equal(ProductStandardPriceState.Active, result.StandardPriceState);
    }

    [Fact]
    public void Evaluate_MissingSourceKeepsExistingApprovedStandardPrice()
    {
        var result = ProductPricingHealthEvaluator.Evaluate(
            source: null,
            draft: null,
            approved: ApprovedPricing(Now.AddDays(-1)),
            now: Now,
            options: new QuotationFeatureOptions());

        Assert.Equal(ProductPricingHealthStatus.NoEligibleSource, result.Status);
        Assert.True(result.RequiresPricingAction);
        Assert.Equal(ProductStandardPriceState.Active, result.StandardPriceState);
    }

    [Fact]
    public void Evaluate_WithoutApprovedVersionAwaitsApproval()
    {
        var result = ProductPricingHealthEvaluator.Evaluate(
            ReadySource(),
            draft: null,
            approved: null,
            now: Now,
            options: new QuotationFeatureOptions());

        Assert.Equal(ProductPricingHealthStatus.AwaitingApproval, result.Status);
        Assert.Equal(ProductStandardPriceState.PendingInitialApproval, result.StandardPriceState);
        Assert.True(result.RequiresPricingAction);
    }

    private static ProductPricingSourceOptionDto ReadySource(decimal currentMaterialCost = 100m)
        => new()
        {
            IsEligible = true,
            IsCurrentMaterialCostComplete = true,
            CurrentMaterialCost = currentMaterialCost,
            PricingStatus = "Available"
        };

    private static PricingVersionRow ApprovedPricing(
        DateTime approvedAt,
        decimal? materialCostSnapshot = null,
        decimal? standardSellingPrice = 200m)
        => new()
        {
            ProductPricingVersionId = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            Status = ProductPricingStatus.Approved,
            Version = 1,
            MaterialCostSnapshot = materialCostSnapshot,
            StandardSellingPrice = standardSellingPrice,
            ApprovedAt = approvedAt,
            CreatedDate = approvedAt
        };
}
