using HRM.Application.Abstractions.Commons.Pricing;
using HRM.Application.Commons.Pricing.Dtos;
using HRM.Application.Commons.Pricing.Models;
using HRM.Application.Commons.Pricing.Services;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Queries.GetProductPricingWorkbench;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Application.Features.PLM.Formulas.Dtos.Commons;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.Formulas;

namespace HRM.Application.Tests.Features.CRM.Quotations;

public sealed class PricingReadContractConsistencyTests
{
    [Fact]
    public void ListDrawerWorkspaceAndPlm_UseSameCanonicalEngineCalculation()
    {
        var policy = new ResolvedFormulaPricingPolicy(
            Guid.NewGuid(),
            7,
            new FormulaPricingPolicyDefinition(
                FormulaPricingProfile.Powder,
                10m,
                20m,
                FormulaPricingRoundingRule.Nearest,
                1m,
                [new("All", null, null, true, true, 5m, 0)]));
        var engine = new FormulaPricingEngine(new EmptyPolicyResolver(), new EmptyMaterialPrices());
        var result = engine.CalculateResolved(
            new PricingEngineRequest
            {
                CompanyId = Guid.NewGuid(),
                Profile = FormulaPricingProfile.Powder,
                Currency = "VND"
            },
            policy,
            new FormulaRealtimeMaterialCostResult(100m, true, 0));
        var canonical = result.Data!.Calculation!;
        var source = new ProductPricingSourceOptionDto
        {
            SourceType = ProductPricingSourceType.Formula,
            SourceId = Guid.NewGuid(),
            PricingStatus = "Available",
            StandardSellingPrice = result.Data.StandardSellingPrice,
            Pricing = canonical,
            PriceTierTemplates = result.Data.SuggestedTiers
        };

        var list = ProductPricingWorkbenchMapper.MapSummary(
            new ProductRow { ProductId = Guid.NewGuid() },
            "VND",
            null,
            null,
            source,
            []);
        var drawer = ProductPricingWorkbenchMapper.BuildEffectivePricing(null, source);
        var workspace = ProductPricingWorkbenchMapper.MapSuggestedTiers(source.PriceTierTemplates);
        var plm = new FormulaInformationDto
        {
            Pricing = result.Data.Calculation,
            StandardSellingPrice = result.Data.StandardSellingPrice,
            SuggestedPriceTiers = result.Data.SuggestedTiers
        };

        Assert.Same(canonical, drawer);
        Assert.Equal(canonical.StandardSellingPrice, list.StandardSellingPrice);
        Assert.Equal(canonical.SuggestedPriceTiers.Single().UnitPrice, workspace.Single().UnitPrice);
        Assert.Same(canonical, plm.Pricing);
        Assert.Equal(7, result.Data.FormulaPricingPolicyVersion);
    }

    private sealed class EmptyPolicyResolver : IFormulaPricingPolicyResolver
    {
        public Task<ResolvedFormulaPricingPolicy?> GetPublishedAsync(
            Guid companyId, Guid categoryId, FormulaPricingProfile profile, string currency,
            CancellationToken cancellationToken) => Task.FromResult<ResolvedFormulaPricingPolicy?>(null);

        public Task<IReadOnlyDictionary<FormulaPricingPolicyLookupKey, ResolvedFormulaPricingPolicy>>
            GetPublishedBatchAsync(
                IEnumerable<FormulaPricingPolicyLookupKey> keys,
                CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyDictionary<FormulaPricingPolicyLookupKey, ResolvedFormulaPricingPolicy>>(
                new Dictionary<FormulaPricingPolicyLookupKey, ResolvedFormulaPricingPolicy>());
    }

    private sealed class EmptyMaterialPrices : IMaterialPriceQueryService
    {
        public Task<Dictionary<Guid, LatestMaterialPriceDto>> LoadLatestMaterialPriceInfoDictAsync(
            IEnumerable<Guid?> materialIds, CancellationToken cancellationToken = default)
            => Task.FromResult(new Dictionary<Guid, LatestMaterialPriceDto>());

        public Task<Dictionary<Guid, LatestMaterialPriceDto>> LoadLatestMaterialPriceInfoBySupplierDictAsync(
            Guid supplierId, IEnumerable<Guid?> materialIds, CancellationToken cancellationToken = default)
            => Task.FromResult(new Dictionary<Guid, LatestMaterialPriceDto>());

        public Task<Dictionary<PriceItemKey, LatestItemPriceDto>> LoadLatestItemPriceInfoDictAsync(
            IEnumerable<PriceItemRequest> items, CancellationToken cancellationToken = default)
            => Task.FromResult(new Dictionary<PriceItemKey, LatestItemPriceDto>());

        public Task<Dictionary<PriceItemKey, LatestItemPriceDto>> LoadLatestPricingItemPriceInfoDictAsync(
            Guid companyId, string currency, IEnumerable<PriceItemRequest> items,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new Dictionary<PriceItemKey, LatestItemPriceDto>());
    }
}
