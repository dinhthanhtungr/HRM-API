using System.Text.Json;
using HRM.Application.Abstractions.Commons.Pricing;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Persistence.Commons.Pricing;
using HRM.Application.Commons.Pricing.Dtos;
using HRM.Application.Commons.Pricing.Models;
using HRM.Application.Commons.Pricing.Services;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Application.Features.Pricing.Authorization;
using HRM.Application.Tests.Features.DevAndQA.IQC;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.Formulas;
using HRM.Infrastructure.Services.Pricing;
using Microsoft.Extensions.Options;

namespace HRM.Application.Tests.Features.CRM.Quotations;

public sealed class SampleTrialPricingSelectionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreferredFormulaKeepsIdenticalVisiblePriceWhileReadingOnlyItsMaterials(bool showCosts)
    {
        var f = new Fixture();
        var access = new PricingAccessDecision(false, true, true, showCosts, showCosts, showCosts, showCosts, false, false);
        var all = await f.Visible.LoadVisibleAsync([f.Product.ProductId], f.Company, "VND", access, default);
        Assert.Equal(30, f.Prices.Items.Count);
        var preferred = f.Formulas[17];
        var expected = all[f.Product.ProductId].Single(x => x.SourceId == preferred.FormulaId);

        var actual = await f.Visible.LoadForSampleTrialsVisibleAsync(
            [(f.Product.ProductId, preferred.FormulaId)], f.Company, "VND", access, default);

        Assert.Single(f.Prices.Items);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(Assert.Single(actual[f.Product.ProductId])));
        Assert.NotNull(expected.StandardSellingPrice);
        Assert.Equal(0, f.Store.Saves);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingOrIneligibleFormulaUsesTheSameCustomerSelectedFallback(bool inactive)
    {
        var f = new Fixture();
        f.Formulas[5].IsSelect = true;
        f.Formulas[5].UpdatedDate = f.Clock.Now.AddYears(-1);
        f.Formulas[17].IsActive = !inactive;
        Guid? preferredId = inactive ? f.Formulas[17].FormulaId : null;
        var all = await f.Realtime.LoadAsync([f.Product.ProductId], f.Company, "VND", default);
        var expected = all[f.Product.ProductId].First(x => x.IsEligible);

        var actual = await f.Realtime.LoadForSampleTrialsAsync(
            [(f.Product.ProductId, preferredId)], f.Company, "VND", default);

        Assert.Equal(f.Formulas[5].FormulaId, expected.SourceId);
        Assert.Single(f.Prices.Items);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(Assert.Single(actual[f.Product.ProductId])));
    }

    [Fact]
    public async Task MultipleTrialsKeepDistinctFormulaPricesAndIgnoreForeignOrUnknownSources()
    {
        var f = new Fixture();
        var foreign = new Product { ProductId = Guid.NewGuid(), CompanyId = Guid.NewGuid(), IsActive = true };
        var foreignFormula = new Formula { FormulaId = Guid.NewGuid(), ProductId = foreign.ProductId,
            Product = foreign, CompanyId = foreign.CompanyId, IsActive = true, Status = "Approved" };
        f.Store.Seed("Formulas", [.. f.Formulas, foreignFormula]);
        var trials = new (Guid ProductId, Guid? FormulaId)[] {
            (f.Product.ProductId, f.Formulas[3].FormulaId),
            (f.Product.ProductId, f.Formulas[4].FormulaId),
            (f.Product.ProductId, f.Formulas[3].FormulaId),
            (f.Product.ProductId, Guid.NewGuid()),
            (foreign.ProductId, foreignFormula.FormulaId),
            (Guid.NewGuid(), null)
        };
        var all = await f.Realtime.LoadAsync(trials.Select(x => x.ProductId).Distinct().ToArray(), f.Company, "VND", default);
        var expected = trials.Where(x => all.ContainsKey(x.ProductId)).Select(x =>
            all[x.ProductId].FirstOrDefault(s => s.SourceType == ProductPricingSourceType.Formula &&
                s.SourceId == x.FormulaId && s.IsEligible) ?? all[x.ProductId].First(s => s.IsEligible))
            .DistinctBy(x => x.SourceId).OrderBy(x => x.SourceId).ToArray();

        var actual = await f.Realtime.LoadForSampleTrialsAsync(trials, f.Company, "VND", default);

        Assert.Single(actual);
        Assert.Equal(3, f.Prices.Items.Count);
        Assert.Equal(JsonSerializer.Serialize(expected),
            JsonSerializer.Serialize(actual[f.Product.ProductId].OrderBy(x => x.SourceId).ToArray()));
    }

    [Fact]
    public async Task ManufacturingFallbackRetainsItsPriorityOverUnselectedDevelopmentFormulas()
    {
        var f = new Fixture();
        var va = new ManufacturingFormula { ManufacturingFormulaId = Guid.NewGuid(), CompanyId = f.Company,
            IsActive = true, Status = "Completed", ExternalId = "VA", UpdatedDate = f.Clock.Now };
        f.Store.Seed("ProductStandardFormulas", new ProductStandardFormula {
            ProductStandardFormulaId = Guid.NewGuid(), ProductId = f.Product.ProductId, Product = f.Product,
            CompanyId = f.Company, ManufacturingFormulaId = va.ManufacturingFormulaId, ManufacturingFormula = va,
            ValidFrom = f.Clock.Now.AddDays(-1) });
        var all = await f.Realtime.LoadAsync([f.Product.ProductId], f.Company, "VND", default);
        var expected = all[f.Product.ProductId].First();
        var actual = await f.Realtime.LoadForSampleTrialsAsync([(f.Product.ProductId, null)], f.Company, "VND", default);
        Assert.Equal(ProductPricingSourceType.ManufacturingFormula, expected.SourceType);
        Assert.Empty(f.Prices.Items);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(Assert.Single(actual[f.Product.ProductId])));
    }

    [Fact]
    public async Task NoRealtimeCapabilitySkipsAllSourceAndPriceReads()
    {
        var service = new ProductPricingSourceQueryService(null!);
        var access = new PricingAccessDecision(false, true, false, false, false, false, false, false, false);
        var actual = await service.LoadForSampleTrialsVisibleAsync([(Guid.NewGuid(), null)], Guid.NewGuid(), "VND", access, default);
        Assert.Empty(actual);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MissingPriceOrPolicyDoesNotReplaceThePreferredFormula(bool missingPrice)
    {
        var f = new Fixture();
        if (missingPrice) f.Prices.MissingIds.Add(f.MaterialIds[17]);
        else f.Policies.Missing = true;
        var preferred = f.Formulas[17];
        var all = await f.Realtime.LoadAsync([f.Product.ProductId], f.Company, "VND", default);
        var expected = all[f.Product.ProductId].Single(x => x.SourceId == preferred.FormulaId);
        if (missingPrice)
        {
            Assert.False(expected.IsCurrentMaterialCostComplete);
            Assert.Equal(1, expected.MissingMaterialPriceCount);
            Assert.NotNull(all[f.Product.ProductId].First().StandardSellingPrice);
        }
        else Assert.Null(expected.StandardSellingPrice);

        var actual = await f.Realtime.LoadForSampleTrialsAsync(
            [(f.Product.ProductId, preferred.FormulaId)], f.Company, "VND", default);

        var selected = Assert.Single(actual[f.Product.ProductId]);
        Assert.Equal(preferred.FormulaId, selected.SourceId);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(selected));
    }

    [Fact]
    public async Task EqualDatesRetainTheExistingCodeTieBreakAndNoEligibleSourceStaysEmpty()
    {
        var f = new Fixture();
        foreach (var formula in f.Formulas) formula.UpdatedDate = f.Clock.Now;
        var all = await f.Realtime.LoadAsync([f.Product.ProductId], f.Company, "VND", default);
        var actual = await f.Realtime.LoadForSampleTrialsAsync([(f.Product.ProductId, null)], f.Company, "VND", default);
        Assert.Equal(f.Formulas[0].FormulaId, all[f.Product.ProductId].First().SourceId);
        Assert.Equal(JsonSerializer.Serialize(all[f.Product.ProductId].First()),
            JsonSerializer.Serialize(Assert.Single(actual[f.Product.ProductId])));

        foreach (var formula in f.Formulas) formula.Status = "Draft";
        Assert.Empty(await f.Realtime.LoadAsync([f.Product.ProductId], f.Company, "VND", default));
        Assert.Empty(await f.Realtime.LoadForSampleTrialsAsync(
            [(f.Product.ProductId, f.Formulas[0].FormulaId)], f.Company, "VND", default));
    }

    [Fact]
    public async Task ProductIngredientsWithRecursiveFallbackKeepTheLegacyPricingBatchResult()
    {
        var f = new Fixture();
        var a = new Product { ProductId = Guid.NewGuid(), CompanyId = f.Company, IsActive = true };
        var b = new Product { ProductId = Guid.NewGuid(), CompanyId = f.Company, IsActive = true };
        Formula Child(Product product, bool selected) => new() {
            FormulaId = Guid.NewGuid(), ProductId = product.ProductId, Product = product,
            CompanyId = f.Company, IsActive = true, IsSelect = selected, Status = "SampleSent", CreatedDate = f.Clock.Now };
        FormulaMaterial ProductLine(Formula formula, Product product) => new() {
            FormulaMaterialId = Guid.NewGuid(), FormulaId = formula.FormulaId, Formula = formula,
            ProductId = product.ProductId, Product = product, itemType = ItemType.Product, Quantity = 1, IsActive = true };
        var aPrimary = Child(a, true);
        var aFallback = Child(a, false);
        var bPrimary = Child(b, true);
        var material = new Material { MaterialId = Guid.NewGuid(), CompanyId = f.Company, Name = "Base" };
        var (priceDb, priceStore) = IqcTestStore.CreateContext<IPriceReadDbContext>();
        priceStore.Seed("Formulas", aPrimary, aFallback, bPrimary);
        priceStore.Seed("FormulaMaterials", ProductLine(aPrimary, b), ProductLine(bPrimary, a), new FormulaMaterial {
            FormulaMaterialId = Guid.NewGuid(), FormulaId = aFallback.FormulaId, Formula = aFallback,
            MaterialId = material.MaterialId, itemType = ItemType.Material, Quantity = 1, IsActive = true });
        priceStore.Seed("MaterialsSuppliers", new MaterialsSupplier { MaterialsSuppliersId = Guid.NewGuid(),
            MaterialId = material.MaterialId, CurrentPrice = 20m, IsActive = true, CreateDate = f.Clock.Now });
        f.Store.Seed("FormulaMaterials", ProductLine(f.Formulas[0], a), ProductLine(f.Formulas[17], b));
        var prices = new MaterialPriceQueryService(priceDb, f.Clock, Options.Create(new QuotationFeatureOptions()));
        var service = new ProductPricingRealtimeSourceQueryService(f.Db, f.Clock, prices, f.Policies,
            new FormulaPricingEngine(f.Policies, prices));
        var all = await service.LoadAsync([f.Product.ProductId], f.Company, "VND", default);
        var expected = all[f.Product.ProductId].Single(x => x.SourceId == f.Formulas[17].FormulaId);
        var actual = await service.LoadForSampleTrialsAsync(
            [(f.Product.ProductId, f.Formulas[17].FormulaId)], f.Company, "VND", default);
        var selected = actual[f.Product.ProductId].Single(x => x.SourceId == f.Formulas[17].FormulaId);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(selected));
    }

    [Theory]
    [InlineData(ItemType.Product)]
    [InlineData(ItemType.ProductFailure)]
    public async Task ManufacturingProductIngredientsAlsoPreserveTheFullPricingBatch(ItemType itemType)
    {
        var f = new Fixture();
        var va = new ManufacturingFormula { ManufacturingFormulaId = Guid.NewGuid(), CompanyId = f.Company,
            IsActive = true, Status = "Completed", ExternalId = "VA", UpdatedDate = f.Clock.Now };
        var component = new Product { ProductId = Guid.NewGuid(), CompanyId = f.Company, IsActive = true };
        f.Store.Seed("ProductStandardFormulas", new ProductStandardFormula {
            ProductStandardFormulaId = Guid.NewGuid(), ProductId = f.Product.ProductId, Product = f.Product,
            CompanyId = f.Company, ManufacturingFormulaId = va.ManufacturingFormulaId, ManufacturingFormula = va,
            ValidFrom = f.Clock.Now.AddDays(-1) });
        f.Store.Seed("ManufacturingFormulaMaterials", new ManufacturingFormulaMaterial {
            ManufacturingFormulaMaterialId = Guid.NewGuid(), ManufacturingFormulaId = va.ManufacturingFormulaId,
            ManufacturingFormula = va, ProductId = component.ProductId, Product = component,
            IsActive = true, itemType = itemType, Quantity = 1 });
        var all = await f.Realtime.LoadAsync([f.Product.ProductId], f.Company, "VND", default);
        var actual = await f.Realtime.LoadForSampleTrialsAsync(
            [(f.Product.ProductId, f.Formulas[17].FormulaId)], f.Company, "VND", default);
        Assert.Equal(31, f.Prices.Items.Count);
        Assert.Equal(JsonSerializer.Serialize(all), JsonSerializer.Serialize(actual));
    }

    private sealed class Fixture
    {
        internal Guid Company { get; } = Guid.NewGuid();
        internal Product Product { get; }
        internal Formula[] Formulas { get; }
        internal Guid[] MaterialIds { get; }
        internal IqcTestStore Store { get; }
        internal IPLMReadDbContext Db { get; }
        internal Prices Prices { get; } = new();
        internal Policies Policies { get; } = new();
        internal Clock Clock { get; } = new();
        internal ProductPricingRealtimeSourceQueryService Realtime { get; }
        internal ProductPricingSourceQueryService Visible { get; }

        internal Fixture()
        {
            var (db, store) = IqcTestStore.CreateContext<IPLMReadDbContext>();
            Db = db;
            Store = store;
            Product = new Product { ProductId = Guid.NewGuid(), CompanyId = Company, CategoryId = Guid.NewGuid(), IsActive = true };
            Formulas = Enumerable.Range(0, 30).Select(i => new Formula { FormulaId = Guid.NewGuid(),
                CompanyId = Company, ProductId = Product.ProductId, Product = Product, IsActive = true, Status = "Approved",
                ExternalId = $"VU_{i:D2}", UpdatedDate = Clock.Now.AddMinutes(i) }).ToArray();
            var materials = Formulas.Select((formula, i) => new Material { MaterialId = Guid.NewGuid(),
                CompanyId = Company, Name = $"Material {i}", ExternalId = $"NVL_{i}" }).ToArray();
            MaterialIds = materials.Select(x => x.MaterialId).ToArray();
            var lines = Formulas.Select((formula, i) => new FormulaMaterial { FormulaMaterialId = Guid.NewGuid(),
                FormulaId = formula.FormulaId, Formula = formula, MaterialId = materials[i].MaterialId, Material = materials[i],
                itemType = ItemType.Material, Quantity = i + 1, IsActive = true }).ToArray();
            store.Seed("Formulas", Formulas);
            store.Seed("FormulaMaterials", lines);
            store.Seed("Materials", materials);
            Realtime = new(db, Clock, Prices, Policies, new FormulaPricingEngine(Policies, Prices));
            Visible = new(Realtime);
        }
    }

    private sealed class Clock : IDateTimeProvider { public DateTime Now => new(2026, 11, 18); }

    private sealed class Policies : IFormulaPricingPolicyResolver
    {
        internal bool Missing { get; set; }
        private readonly ResolvedFormulaPricingPolicy _policy = new(Guid.NewGuid(), 1,
            new FormulaPricingPolicyDefinition(FormulaPricingProfile.Powder, 10m, 20m,
                FormulaPricingRoundingRule.Nearest, 1m, [new("All", null, null, true, true, 5m, 0)]));
        public Task<ResolvedFormulaPricingPolicy?> GetPublishedAsync(Guid companyId, Guid categoryId,
            FormulaPricingProfile profile, string currency, CancellationToken ct) => Task.FromResult<ResolvedFormulaPricingPolicy?>(_policy);
        public Task<IReadOnlyDictionary<FormulaPricingPolicyLookupKey, ResolvedFormulaPricingPolicy>> GetPublishedBatchAsync(
            IEnumerable<FormulaPricingPolicyLookupKey> keys, CancellationToken ct)
            => Task.FromResult<IReadOnlyDictionary<FormulaPricingPolicyLookupKey, ResolvedFormulaPricingPolicy>>(
                Missing ? new Dictionary<FormulaPricingPolicyLookupKey, ResolvedFormulaPricingPolicy>() : keys.Distinct().ToDictionary(x => x, _ => _policy));
    }

    private sealed class Prices : IMaterialPriceQueryService
    {
        internal List<PriceItemRequest> Items { get; private set; } = [];
        internal HashSet<Guid> MissingIds { get; } = [];
        public Task<Dictionary<PriceItemKey, LatestItemPriceDto>> LoadLatestPricingItemPriceInfoDictAsync(
            Guid companyId, string currency, IEnumerable<PriceItemRequest> items, CancellationToken ct = default)
        {
            Items = items.ToList();
            return Task.FromResult(Items.Where(x => !MissingIds.Contains(x.MaterialId ?? x.ProductId!.Value))
                .ToDictionary(x => new PriceItemKey(x.ItemType, x.MaterialId ?? x.ProductId!.Value),
                x => new LatestItemPriceDto { ItemType = x.ItemType, ItemId = x.MaterialId ?? x.ProductId!.Value,
                    CurrentPrice = 100m, PriceSource = LatestPriceSourceType.PurchaseOrder }));
        }
        public Task<Dictionary<Guid, LatestMaterialPriceDto>> LoadLatestMaterialPriceInfoDictAsync(IEnumerable<Guid?> ids, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Dictionary<Guid, LatestMaterialPriceDto>> LoadLatestMaterialPriceInfoBySupplierDictAsync(Guid supplier, IEnumerable<Guid?> ids, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Dictionary<PriceItemKey, LatestItemPriceDto>> LoadLatestItemPriceInfoDictAsync(IEnumerable<PriceItemRequest> items, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
