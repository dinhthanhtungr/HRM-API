using HRM.Application.Commons.Concurrency;
using HRM.Application.Commons.Pricing.Dtos;
using HRM.Application.Commons.Pricing.Models;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.Formulas;

namespace HRM.Application.Tests.Features.CRM.Quotations;

public sealed class ProductPricingVersionPolicyRulesTests
{
    [Fact]
    public void Calculate_MissingMaterialCost_UsesZeroInsteadOfBlocking()
    {
        var calculation = ProductPricingVersionPolicyRules.Calculate(
            PolicyDefinition(),
            realtimeMaterialCost: null,
            manufacturingCost: null,
            standardSellingPrice: null,
            profitMarginRate: null,
            changedField: null);

        Assert.True(calculation.Success);
        Assert.Equal(0m, calculation.Data!.MaterialCost);
        Assert.Equal(10m, calculation.Data.ManufacturingCost);
    }

    [Fact]
    public void Create_UsesRealtimeSnapshotAndPolicyGeneratedTiers()
    {
        var calculation = ProductPricingVersionPolicyRules.Calculate(
            PolicyDefinition(),
            realtimeMaterialCost: 120m,
            manufacturingCost: null,
            standardSellingPrice: null,
            profitMarginRate: null,
            changedField: null);

        Assert.True(calculation.Success);
        Assert.Equal(120m, calculation.Data!.MaterialCost);
        Assert.Equal(10m, calculation.Data.ManufacturingCost);
        Assert.Equal(156m, calculation.Data.StandardSellingPrice);

        var tiers = ProductPricingVersionPolicyRules.BuildPolicyTiers(
            Guid.NewGuid(),
            calculation.Data.SuggestedPriceTiers,
            []);
        Assert.True(tiers.Success);
        Assert.False(tiers.Data!.HasManualTierAdjustment);
        Assert.Equal(161m, tiers.Data.Tiers.Single().UnitPrice);
    }

    [Fact]
    public void Update_ResolvesThePolicyAttachedToTheDraft()
    {
        var now = new DateTime(2026, 8, 21);
        var attached = PublishedPolicy(now, version: 3);
        var draft = Version(attached);

        var resolved = ProductPricingVersionPolicyRules.ResolveAttachedPolicy(draft, now);

        Assert.True(resolved.Success);
        Assert.Equal(attached.FormulaPricingPolicyId, resolved.Data!.FormulaPricingPolicyId);
        Assert.Equal(3, resolved.Data.Version);
    }

    [Fact]
    public void Approve_RejectsSupersededAndLegacyPoliciesWithConflict()
    {
        var now = new DateTime(2026, 8, 21);
        var superseded = PublishedPolicy(now, version: 2);
        superseded.Status = FormulaPricingPolicyStatus.Superseded;
        var supersededResult = ProductPricingVersionPolicyRules.ResolveAttachedPolicy(
            Version(superseded),
            now);
        var legacyResult = ProductPricingVersionPolicyRules.ResolveAttachedPolicy(
            new ProductPricingVersion
            {
                CompanyId = Guid.NewGuid(),
                Currency = "VND",
                Product = new Product { ColourCode = "POWDER" }
            },
            now);

        Assert.False(supersededResult.Success);
        Assert.False(legacyResult.Success);
        Assert.Contains(OptimisticConcurrencyHelper.ConflictMessageMarker, supersededResult.Message);
        Assert.Contains(OptimisticConcurrencyHelper.ConflictMessageMarker, legacyResult.Message);
    }

    [Fact]
    public void ManualTierPrice_SetsManualAdjustmentAndKeepsPolicyRange()
    {
        var tiers = ProductPricingVersionPolicyRules.BuildPolicyTiers(
            Guid.NewGuid(),
            [
                new FormulaSuggestedPriceTierDto
                {
                    QuantityRangeLabel = "> 5 tấn",
                    MinQuantity = 5000m,
                    MinInclusive = false,
                    MaxInclusive = true,
                    RequiresManualPrice = true,
                    SortOrder = 0
                }
            ],
            [
                new ProductPricingTierRequest
                {
                    QuantityRangeLabel = "> 5 tấn",
                    MinQuantity = 5000m,
                    MinInclusive = false,
                    MaxInclusive = true,
                    UnitPrice = 180m,
                    SortOrder = 0
                }
            ]);

        Assert.True(tiers.Success);
        Assert.True(tiers.Data!.HasManualTierAdjustment);
        Assert.Equal(180m, tiers.Data.Tiers.Single().UnitPrice);
    }

    [Fact]
    public void SnapshotDto_ExposesSourcePolicyVersionAndCalculatedValues()
    {
        var policy = PublishedPolicy(new DateTime(2026, 8, 21), version: 5);
        var entity = Version(policy);
        entity.ProductPricingVersionId = Guid.NewGuid();
        entity.ProductId = Guid.NewGuid();
        entity.SourceFormulaId = Guid.NewGuid();
        entity.FormulaExternalIdSnapshot = "VU_001";
        entity.MaterialCostSnapshot = 100m;
        entity.ManufacturingCost = 10m;
        entity.StandardSellingPrice = 132m;
        entity.ProfitMarginRate = 20m;
        entity.CalculatedAt = new DateTime(2026, 8, 21, 10, 0, 0);

        var dto = ProductPricingVersionMapper.ToDto(entity);

        Assert.Equal(policy.FormulaPricingPolicyId, dto.FormulaPricingPolicyId);
        Assert.Equal(5, dto.FormulaPricingPolicyVersion);
        Assert.Equal(entity.SourceFormulaId, dto.SourceFormulaId);
        Assert.Equal("VU_001", dto.FormulaExternalIdSnapshot);
        Assert.Equal(100m, dto.MaterialCostSnapshot);
        Assert.Equal(entity.CalculatedAt, dto.CalculatedAt);
    }

    private static FormulaPricingPolicyDefinition PolicyDefinition()
        => new(
            FormulaPricingProfile.Powder,
            10m,
            20m,
            FormulaPricingRoundingRule.Nearest,
            1m,
            [new("All", null, null, true, true, 5m, 0)]);

    private static FormulaPricingPolicy PublishedPolicy(DateTime now, int version)
    {
        var policyId = Guid.NewGuid();
        return new FormulaPricingPolicy
        {
            FormulaPricingPolicyId = policyId,
            CompanyId = Guid.NewGuid(),
            Profile = FormulaPricingProfile.Powder,
            Currency = "VND",
            Version = version,
            DefaultManufacturingCost = 10m,
            DefaultProfitMarginRate = 20m,
            RoundingRule = FormulaPricingRoundingRule.Nearest,
            RoundingIncrement = 1m,
            Status = FormulaPricingPolicyStatus.Published,
            EffectiveFrom = now.AddDays(-1),
            IsActive = true,
            Tiers =
            [
                new FormulaPricingPolicyTier
                {
                    FormulaPricingPolicyId = policyId,
                    QuantityRangeLabel = "All",
                    MinInclusive = true,
                    MaxInclusive = true,
                    PriceOffset = 5m,
                    SortOrder = 0
                }
            ]
        };
    }

    private static ProductPricingVersion Version(FormulaPricingPolicy policy)
        => new()
        {
            FormulaPricingPolicyId = policy.FormulaPricingPolicyId,
            FormulaPricingPolicy = policy,
            CompanyId = policy.CompanyId,
            Currency = policy.Currency,
            Product = new Product
            {
                ColourCode = policy.Profile == FormulaPricingProfile.Compound
                    ? "COMPOUND-C"
                    : "POWDER"
            }
        };
}
