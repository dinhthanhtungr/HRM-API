using HRM.Application.Abstractions.Commons.Pricing;
using HRM.Application.Commons.Pricing.Dtos;
using HRM.Application.Commons.Pricing.Models;
using HRM.Application.Commons.Pricing.Services;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.Formulas;

namespace HRM.Application.Tests.Features.CRM.Quotations;

public sealed class FormulaPricingEngineTests
{
    [Fact]
    public async Task ResolveAsync_UsesPolicyRoundingAndKeepsManualTierEmpty()
    {
        var companyId = Guid.NewGuid();
        var policy = new ResolvedFormulaPricingPolicy(Guid.NewGuid(), 4, new FormulaPricingPolicyDefinition(
            FormulaPricingProfile.Powder, 10m, 20m, FormulaPricingRoundingRule.Up, 10m,
            [new("Auto", null, 100m, true, false, 5m, 0), new("Manual", 100m, null, true, true, null, 1)]));
        var engine = new FormulaPricingEngine(new PolicyResolver(policy), new MaterialPrices());

        var result = await engine.ResolveAsync(new PricingEngineRequest
        {
            CompanyId = companyId, Profile = FormulaPricingProfile.Powder, Currency = "vnd",
            MaterialCost = 101m
        }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(110m, result.Data!.MaterialCost);
        Assert.Equal(150m, result.Data.StandardSellingPrice);
        Assert.Equal(4, result.Data.FormulaPricingPolicyVersion);
        Assert.Equal(160m, result.Data.SuggestedTiers[0].UnitPrice);
        Assert.Null(result.Data.SuggestedTiers[1].UnitPrice);
        Assert.True(result.Data.SuggestedTiers[1].RequiresManualPrice);
    }

    [Fact]
    public async Task ResolveAsync_ReturnsPolicyMissingAndIncompleteMaterialStates()
    {
        var companyId = Guid.NewGuid();
        var missing = new FormulaPricingEngine(new PolicyResolver(null), new MaterialPrices());
        var noPolicy = await missing.ResolveAsync(new PricingEngineRequest
        { CompanyId = companyId, Profile = FormulaPricingProfile.Compound, Currency = "USD", MaterialCost = 1m }, CancellationToken.None);
        Assert.False(noPolicy.Success);
        Assert.Equal("PricingPolicyMissing", noPolicy.Message);

        var policy = new ResolvedFormulaPricingPolicy(Guid.NewGuid(), 1, new FormulaPricingPolicyDefinition(
            FormulaPricingProfile.Compound, 1m, 0m, FormulaPricingRoundingRule.Nearest, 1m,
            [new("Manual", null, null, true, true, null, 0)]));
        var incomplete = await new FormulaPricingEngine(new PolicyResolver(policy), new MaterialPrices())
            .ResolveAsync(new PricingEngineRequest
            {
                CompanyId = companyId, Profile = FormulaPricingProfile.Compound, Currency = "USD",
                MaterialItems = [new FormulaMaterialCostItem(Guid.NewGuid(), ItemType.Material, 1m)]
            }, CancellationToken.None);
        Assert.True(incomplete.Success);
        Assert.False(incomplete.Data!.IsMaterialCostComplete);
        Assert.Equal(1, incomplete.Data.MissingMaterialPriceCount);
    }

    private sealed class PolicyResolver(ResolvedFormulaPricingPolicy? policy) : IFormulaPricingPolicyResolver
    {
        public Task<ResolvedFormulaPricingPolicy?> GetPublishedAsync(Guid companyId, FormulaPricingProfile profile, string currency, CancellationToken cancellationToken) => Task.FromResult(policy);
        public Task<IReadOnlyDictionary<FormulaPricingPolicyLookupKey, ResolvedFormulaPricingPolicy>> GetPublishedBatchAsync(IEnumerable<FormulaPricingPolicyLookupKey> keys, CancellationToken cancellationToken)
        {
            var key = keys.Single();
            return Task.FromResult<IReadOnlyDictionary<FormulaPricingPolicyLookupKey, ResolvedFormulaPricingPolicy>>(policy is null ? new Dictionary<FormulaPricingPolicyLookupKey, ResolvedFormulaPricingPolicy>() : new Dictionary<FormulaPricingPolicyLookupKey, ResolvedFormulaPricingPolicy> { [key] = policy });
        }
    }

    private sealed class MaterialPrices : IMaterialPriceQueryService
    {
        public Task<Dictionary<Guid, LatestMaterialPriceDto>> LoadLatestMaterialPriceInfoDictAsync(IEnumerable<Guid?> materialIds, CancellationToken cancellationToken = default) => Task.FromResult(new Dictionary<Guid, LatestMaterialPriceDto>());
        public Task<Dictionary<Guid, LatestMaterialPriceDto>> LoadLatestMaterialPriceInfoBySupplierDictAsync(Guid supplierId, IEnumerable<Guid?> materialIds, CancellationToken cancellationToken = default) => Task.FromResult(new Dictionary<Guid, LatestMaterialPriceDto>());
        public Task<Dictionary<PriceItemKey, LatestItemPriceDto>> LoadLatestItemPriceInfoDictAsync(IEnumerable<PriceItemRequest> items, CancellationToken cancellationToken = default) => Task.FromResult(new Dictionary<PriceItemKey, LatestItemPriceDto>());
    }
}
