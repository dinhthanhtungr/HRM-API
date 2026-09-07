using HRM.Application.Abstractions.Commons.Pricing;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Commons.Pricing.Dtos;
using HRM.Application.Commons.Pricing.Helpers;
using HRM.Application.Commons.Pricing.Models;
using HRM.Application.Commons.Pricing.Services;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.PLM.Formulas.Helpers;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.Formulas;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Services;

/// <summary>
/// Resolves eligible pricing sources together with their latest item prices.
/// Formula totals are deliberately not treated as current material costs.
/// </summary>
internal sealed class ProductPricingRealtimeSourceQueryService
{
    private readonly IPLMReadDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IMaterialPriceQueryService _materialPriceQueryService;
    private readonly IFormulaPricingPolicyResolver _pricingPolicyResolver;
    private readonly FormulaPricingEngine _pricingEngine;

    public ProductPricingRealtimeSourceQueryService(
        IPLMReadDbContext dbContext,
        IDateTimeProvider dateTimeProvider,
        IMaterialPriceQueryService materialPriceQueryService,
        IFormulaPricingPolicyResolver pricingPolicyResolver,
        FormulaPricingEngine pricingEngine)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
        _materialPriceQueryService = materialPriceQueryService;
        _pricingPolicyResolver = pricingPolicyResolver;
        _pricingEngine = pricingEngine;
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<ProductPricingSourceOptionDto>>> LoadAsync(
        IReadOnlyCollection<Guid> productIds,
        Guid companyId,
        string currency,
        CancellationToken cancellationToken)
    {
        if (productIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<ProductPricingSourceOptionDto>>();
        }

        var sourceRows = await LoadSourceRowsAsync(productIds, companyId, cancellationToken);
        if (sourceRows.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<ProductPricingSourceOptionDto>>();
        }

        var materialRows = await LoadMaterialRowsAsync(sourceRows, companyId, cancellationToken);
        var latestPriceByItem = await LoadLatestPricesAsync(
            materialRows, companyId, currency, cancellationToken);
        var pricingPolicies = await LoadPricingPoliciesAsync(sourceRows, companyId, currency, cancellationToken);
        var materialsBySource = materialRows
            .GroupBy(x => x.SourceKey)
            .ToDictionary(
                x => x.Key,
                x => (IReadOnlyList<QuotationProductPricingMaterialDto>)x
                    .OrderBy(y => y.LineNo)
                    .Select(y => MapMaterial(y, latestPriceByItem))
                    .ToArray());
        var realtimeCostBySource = materialRows
            .GroupBy(x => x.SourceKey)
            .ToDictionary(
                x => x.Key,
                x => FormulaRealtimeMaterialCostCalculator.Calculate(
                    x.Select(y => new FormulaMaterialCostItem(
                        y.ItemId,
                        y.ItemType,
                        y.Quantity)),
                    latestPriceByItem));

        return sourceRows
            .GroupBy(x => x.ProductId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ProductPricingSourceOptionDto>)group
                    .OrderByDescending(x => x.IsCustomerSelected)
                    .ThenBy(x => x.SourceType)
                    .ThenByDescending(x => x.UpdatedDate)
                    .ThenBy(x => x.ExternalId)
                    .Select(x => MapSource(
                        x,
                        realtimeCostBySource.GetValueOrDefault(x.Key)
                            ?? new FormulaRealtimeMaterialCostResult(null, false, 0),
                        materialsBySource.GetValueOrDefault(x.Key) ?? [],
                        x.PricingProfile.HasValue
                            ? pricingPolicies.GetValueOrDefault(new FormulaPricingPolicyLookupKey(
                                companyId, x.ProductCategoryId, x.PricingProfile.Value, currency.Trim().ToUpperInvariant()))
                            : null,
                        companyId,
                        currency))
                    .ToArray());
    }

    public async Task<IReadOnlyDictionary<ProductPricingSourceSelection, ProductPricingSourceOptionDto>>
        LoadSelectedAsync(
            IReadOnlyCollection<ProductPricingSourceSelection> selections,
            Guid companyId,
            string currency,
            CancellationToken cancellationToken)
    {
        var normalizedSelections = selections
            .Where(x =>
                x.ProductId != Guid.Empty &&
                x.SourceId != Guid.Empty &&
                Enum.IsDefined(x.SourceType))
            .Distinct()
            .ToArray();
        if (normalizedSelections.Length == 0)
        {
            return new Dictionary<ProductPricingSourceSelection, ProductPricingSourceOptionDto>();
        }

        var sourceRows = await LoadSelectedSourceRowsAsync(
            normalizedSelections,
            companyId,
            cancellationToken);
        if (sourceRows.Count == 0)
        {
            return new Dictionary<ProductPricingSourceSelection, ProductPricingSourceOptionDto>();
        }

        var materialRows = await LoadMaterialRowsAsync(sourceRows, companyId, cancellationToken);
        var latestPriceByItem = await LoadLatestPricesAsync(
            materialRows, companyId, currency, cancellationToken);
        var pricingPolicies = await LoadPricingPoliciesAsync(sourceRows, companyId, currency, cancellationToken);
        var materialsBySource = materialRows
            .GroupBy(x => x.SourceKey)
            .ToDictionary(
                x => x.Key,
                x => (IReadOnlyList<QuotationProductPricingMaterialDto>)x
                    .OrderBy(y => y.LineNo)
                    .Select(y => MapMaterial(y, latestPriceByItem))
                    .ToArray());
        var realtimeCostBySource = materialRows
            .GroupBy(x => x.SourceKey)
            .ToDictionary(
                x => x.Key,
                x => FormulaRealtimeMaterialCostCalculator.Calculate(
                    x.Select(y => new FormulaMaterialCostItem(
                        y.ItemId,
                        y.ItemType,
                        y.Quantity)),
                    latestPriceByItem));

        return sourceRows.ToDictionary(
            x => new ProductPricingSourceSelection(x.ProductId, x.SourceType, x.SourceId),
            x => MapSource(
                x,
                realtimeCostBySource.GetValueOrDefault(x.Key)
                    ?? new FormulaRealtimeMaterialCostResult(null, false, 0),
                materialsBySource.GetValueOrDefault(x.Key) ?? [],
                x.PricingProfile.HasValue
                    ? pricingPolicies.GetValueOrDefault(new FormulaPricingPolicyLookupKey(
                        companyId, x.ProductCategoryId, x.PricingProfile.Value, currency.Trim().ToUpperInvariant()))
                    : null,
                companyId,
                currency));
    }

    private async Task<IReadOnlyList<SourceRow>> LoadSourceRowsAsync(
        IReadOnlyCollection<Guid> productIds,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var formulaSources = await _dbContext.Formulas
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                x.Product.IsActive &&
                x.Product.CompanyId == companyId &&
                productIds.Contains(x.ProductId) &&
                ProductPricingSourceRules.EligibleFormulaStatuses.Contains(x.Status))
            .Select(x => new SourceRow
            {
                ProductId = x.ProductId,
                ProductCategoryId = x.Product.CategoryId,
                ProductColourCode = x.Product.ColourCode,
                ProductCode = x.Product.Code,
                ProductAdditive = x.Product.Additive,
                SourceType = ProductPricingSourceType.Formula,
                SourceId = x.FormulaId,
                ExternalId = x.ExternalId,
                Name = x.Name,
                Status = x.Status,
                IsEligible = true,
                IsCustomerSelected = x.IsSelect,
                ManufacturingCost = x.ProductionPrice,
                UpdatedDate = x.UpdatedDate ?? x.CreatedDate
            })
            .ToListAsync(cancellationToken);

        var now = _dateTimeProvider.Now;
        var manufacturingSources = await _dbContext.ProductStandardFormulas
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == companyId &&
                productIds.Contains(x.ProductId) &&
                x.ValidFrom <= now &&
                (!x.ValidTo.HasValue || x.ValidTo >= now) &&
                x.ManufacturingFormulaId.HasValue &&
                x.ManufacturingFormula != null &&
                x.ManufacturingFormula.IsActive &&
                x.ManufacturingFormula.CompanyId == companyId &&
                (ProductPricingSourceRules.EligibleManufacturingFormulaStatuses.Contains(
                     x.ManufacturingFormula.Status) ||
                 x.ManufacturingFormula.ManufacturingFormulaVersions.Any(version =>
                     version.Status == ProductPricingSourceRules.ReleasedManufacturingVersionStatus)))
            .Select(x => new SourceRow
            {
                ProductId = x.ProductId,
                ProductCategoryId = x.Product.CategoryId,
                ProductColourCode = x.Product.ColourCode,
                ProductCode = x.Product.Code,
                ProductAdditive = x.Product.Additive,
                SourceType = ProductPricingSourceType.ManufacturingFormula,
                SourceId = x.ManufacturingFormulaId!.Value,
                ExternalId = x.ManufacturingFormula!.ExternalId,
                Name = x.ManufacturingFormula.Name,
                Status = x.ManufacturingFormula.Status,
                IsEligible = true,
                IsCustomerSelected = true,
                UpdatedDate = x.ManufacturingFormula.UpdatedDate
            })
            .ToListAsync(cancellationToken);

        ApplyLegacyPricingProfiles(formulaSources);
        ApplyLegacyPricingProfiles(manufacturingSources);
        return formulaSources
            .Concat(manufacturingSources)
            .GroupBy(x => x.Key)
            .Select(x => x.First())
            .ToArray();
    }

    private async Task<IReadOnlyList<SourceRow>> LoadSelectedSourceRowsAsync(
        IReadOnlyCollection<ProductPricingSourceSelection> selections,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var productIds = selections.Select(x => x.ProductId).Distinct().ToArray();
        var formulaIds = selections
            .Where(x => x.SourceType == ProductPricingSourceType.Formula)
            .Select(x => x.SourceId)
            .Distinct()
            .ToArray();
        var manufacturingFormulaIds = selections
            .Where(x => x.SourceType == ProductPricingSourceType.ManufacturingFormula)
            .Select(x => x.SourceId)
            .Distinct()
            .ToArray();

        var formulaSources = formulaIds.Length == 0
            ? []
            : await _dbContext.Formulas
                .AsNoTracking()
                .Where(x =>
                    x.Product.IsActive &&
                    x.Product.CompanyId == companyId &&
                    productIds.Contains(x.ProductId) &&
                    formulaIds.Contains(x.FormulaId))
                .Select(x => new SourceRow
                {
                ProductId = x.ProductId,
                ProductCategoryId = x.Product.CategoryId,
                ProductColourCode = x.Product.ColourCode,
                    ProductCode = x.Product.Code,
                    ProductAdditive = x.Product.Additive,
                    SourceType = ProductPricingSourceType.Formula,
                    SourceId = x.FormulaId,
                    ExternalId = x.ExternalId,
                    Name = x.Name,
                    Status = x.Status,
                    IsEligible = x.IsActive &&
                        ProductPricingSourceRules.EligibleFormulaStatuses.Contains(x.Status),
                    IsCustomerSelected = x.IsSelect,
                    ManufacturingCost = x.ProductionPrice,
                    UpdatedDate = x.UpdatedDate ?? x.CreatedDate
                })
                .ToListAsync(cancellationToken);

        var now = _dateTimeProvider.Now;
        var manufacturingSources = manufacturingFormulaIds.Length == 0
            ? []
            : await _dbContext.ProductStandardFormulas
                .AsNoTracking()
                .Where(x =>
                    x.CompanyId == companyId &&
                    productIds.Contains(x.ProductId) &&
                    x.ManufacturingFormulaId.HasValue &&
                    manufacturingFormulaIds.Contains(x.ManufacturingFormulaId.Value) &&
                    x.ManufacturingFormula != null &&
                    x.ManufacturingFormula.CompanyId == companyId)
                .Select(x => new SourceRow
                {
                ProductId = x.ProductId,
                ProductCategoryId = x.Product.CategoryId,
                ProductColourCode = x.Product.ColourCode,
                    ProductCode = x.Product.Code,
                    ProductAdditive = x.Product.Additive,
                    SourceType = ProductPricingSourceType.ManufacturingFormula,
                    SourceId = x.ManufacturingFormulaId!.Value,
                    ExternalId = x.ManufacturingFormula!.ExternalId,
                    Name = x.ManufacturingFormula.Name,
                    Status = x.ManufacturingFormula.Status,
                    IsEligible = x.ManufacturingFormula.IsActive &&
                        (ProductPricingSourceRules.EligibleManufacturingFormulaStatuses.Contains(
                             x.ManufacturingFormula.Status) ||
                         x.ManufacturingFormula.ManufacturingFormulaVersions.Any(version =>
                             version.Status == ProductPricingSourceRules.ReleasedManufacturingVersionStatus)),
                    IsCustomerSelected = x.ValidFrom <= now &&
                        (!x.ValidTo.HasValue || x.ValidTo >= now),
                    UpdatedDate = x.ManufacturingFormula.UpdatedDate
                })
                .ToListAsync(cancellationToken);

        ApplyLegacyPricingProfiles(formulaSources);
        ApplyLegacyPricingProfiles(manufacturingSources);
        var requestedKeys = selections.ToHashSet();
        return formulaSources
            .Concat(manufacturingSources)
            .Where(x => requestedKeys.Contains(
                new ProductPricingSourceSelection(x.ProductId, x.SourceType, x.SourceId)))
            .GroupBy(x => new { x.ProductId, x.SourceType, x.SourceId })
            .Select(x => x
                .OrderByDescending(y => y.IsCustomerSelected)
                .ThenByDescending(y => y.UpdatedDate)
                .First())
            .ToArray();
    }

    private async Task<IReadOnlyList<MaterialRow>> LoadMaterialRowsAsync(
        IReadOnlyCollection<SourceRow> sources,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var formulaIds = sources
            .Where(x => x.SourceType == ProductPricingSourceType.Formula)
            .Select(x => x.SourceId)
            .Distinct()
            .ToArray();
        var manufacturingFormulaIds = sources
            .Where(x => x.SourceType == ProductPricingSourceType.ManufacturingFormula)
            .Select(x => x.SourceId)
            .Distinct()
            .ToArray();

        var formulaMaterials = formulaIds.Length == 0
            ? []
            : await _dbContext.FormulaMaterials
                .AsNoTracking()
                .Where(x =>
                    x.IsActive &&
                    formulaIds.Contains(x.FormulaId) &&
                    x.Formula.Product.IsActive &&
                    x.Formula.Product.CompanyId == companyId)
                .Select(x => new MaterialRow
                {
                    SourceType = ProductPricingSourceType.Formula,
                    SourceId = x.FormulaId,
                    SourceMaterialId = x.FormulaMaterialId,
                    CategoryId = x.CategoryId,
                    ItemId = x.itemType == ItemType.Material || x.itemType == ItemType.MaterialFailure
                        ? x.Material != null && x.Material.CompanyId == companyId ? x.MaterialId : null
                        : x.Product != null && x.Product.CompanyId == companyId ? x.ProductId : null,
                    ItemType = x.itemType,
                    ItemCode = x.MaterialExternalIdSnapshot ?? string.Empty,
                    ItemName = x.MaterialNameSnapshot ?? string.Empty,
                    Quantity = x.Quantity,
                    Unit = x.Unit ?? string.Empty,
                    LineNo = x.LineNo
                })
                .ToListAsync(cancellationToken);

        var manufacturingMaterials = manufacturingFormulaIds.Length == 0
            ? []
            : await _dbContext.ManufacturingFormulaMaterials
                .AsNoTracking()
                .Where(x =>
                    x.IsActive &&
                    manufacturingFormulaIds.Contains(x.ManufacturingFormulaId) &&
                    x.ManufacturingFormula.CompanyId == companyId)
                .Select(x => new MaterialRow
                {
                    SourceType = ProductPricingSourceType.ManufacturingFormula,
                    SourceId = x.ManufacturingFormulaId,
                    SourceMaterialId = x.ManufacturingFormulaMaterialId,
                    CategoryId = x.CategoryId,
                    ItemId = x.itemType == ItemType.Material || x.itemType == ItemType.MaterialFailure
                        ? x.Material != null && x.Material.CompanyId == companyId ? x.MaterialId : null
                        : x.Product != null && x.Product.CompanyId == companyId ? x.ProductId : null,
                    ItemType = x.itemType,
                    ItemCode = x.MaterialExternalIdSnapshot ?? string.Empty,
                    ItemName = x.MaterialNameSnapshot ?? string.Empty,
                    Quantity = x.Quantity,
                    Unit = x.Unit ?? string.Empty,
                    LineNo = x.LineNo
                })
                .ToListAsync(cancellationToken);

        var rows = formulaMaterials.Concat(manufacturingMaterials).ToList();
        var currentItemData = await FormulaItemDisplayResolver.LoadCurrentDataAsync(
            _dbContext,
            companyId,
            rows
                .Where(x => x.ItemId.HasValue)
                .Select(x => new FormulaItemDisplaySource(
                    x.ItemId!.Value,
                    x.ItemType,
                    x.ItemName,
                    x.ItemCode)),
            cancellationToken);

        foreach (var row in rows.Where(x => x.ItemId.HasValue))
        {
            var display = FormulaItemDisplayResolver.Resolve(
                new FormulaItemDisplaySource(
                    row.ItemId!.Value,
                    row.ItemType,
                    row.ItemName,
                    row.ItemCode),
                currentItemData);
            row.ItemName = display.Name ?? string.Empty;
            row.ItemCode = display.ExternalId ?? string.Empty;
        }

        return rows;
    }

    private async Task<Dictionary<PriceItemKey, LatestItemPriceDto>> LoadLatestPricesAsync(
        IReadOnlyCollection<MaterialRow> materialRows,
        Guid companyId,
        string currency,
        CancellationToken cancellationToken)
    {
        var requests = materialRows
            .Where(x => x.ItemId.HasValue && x.ItemId.Value != Guid.Empty)
            .Select(x =>
            {
                var itemType = FormulaRealtimeMaterialCostCalculator.NormalizeItemType(x.ItemType);
                var isMaterial = itemType == ItemType.Material;
                return new PriceItemRequest
                {
                    ItemType = itemType,
                    MaterialId = isMaterial ? x.ItemId : null,
                    ProductId = isMaterial ? null : x.ItemId
                };
            })
            .GroupBy(x => new { x.ItemType, x.MaterialId, x.ProductId })
            .Select(x => x.First())
            .ToArray();

        return await _materialPriceQueryService.LoadLatestPricingItemPriceInfoDictAsync(
            companyId,
            currency,
            requests,
            cancellationToken);
    }

    private ProductPricingSourceOptionDto MapSource(
        SourceRow source,
        FormulaRealtimeMaterialCostResult realtimeCost,
        IReadOnlyList<QuotationProductPricingMaterialDto> materials,
        ResolvedFormulaPricingPolicy? pricingPolicy,
        Guid companyId,
        string currency)
    {
        var engineResult = source.PricingProfile.HasValue && pricingPolicy is not null
            ? _pricingEngine.CalculateResolved(
                new PricingEngineRequest
                {
                    CompanyId = companyId,
                    CategoryId = source.ProductCategoryId,
                    ProductId = source.ProductId,
                    SourceId = source.SourceId,
                    SourceType = source.SourceType.ToString(),
                    Profile = source.PricingProfile,
                    Currency = currency,
                    MaterialCost = realtimeCost.MaterialCost,
                    ManufacturingCostOverride = source.ManufacturingCost
                },
                pricingPolicy,
                realtimeCost)
            : null;
        var resolved = engineResult is { Success: true } ? engineResult.Data : null;
        var pricingStatus = !source.PricingProfile.HasValue || pricingPolicy is null
            ? FormulaPricingPolicyRules.PricingPolicyMissing
            : !realtimeCost.IsComplete
                ? "MaterialPriceMissing"
                : engineResult is { Success: false }
                    ? engineResult.Message ?? "PricingInvalid"
                    : "Available";

        return new ProductPricingSourceOptionDto
        {
            PricingStatus = pricingStatus,
            FormulaPricingPolicyId = pricingPolicy?.FormulaPricingPolicyId,
            FormulaPricingPolicyVersion = pricingPolicy?.Version,
            SourceType = source.SourceType,
            SourceId = source.SourceId,
            ExternalId = source.ExternalId,
            Name = source.Name,
            Status = source.Status,
            IsEligible = source.IsEligible,
            IsCustomerSelected = source.IsCustomerSelected,
            MaterialCostSnapshot = realtimeCost.MaterialCost,
            CurrentMaterialCost = realtimeCost.MaterialCost,
            IsCurrentMaterialCostComplete = realtimeCost.IsComplete,
            MissingMaterialPriceCount = realtimeCost.MissingPriceCount,
            ManufacturingCost = resolved?.ManufacturingCost,
            UsedDefaultManufacturingCost = resolved?.Calculation?.UsedDefaultManufacturingCost == true,
            StandardSellingPrice = resolved?.StandardSellingPrice,
            ProfitMarginRate = resolved?.ProfitMarginRate,
            PricingProfile = source.PricingProfile,
            PriceTierTemplates = resolved?.SuggestedTiers ?? [],
            Pricing = resolved?.Calculation,
            UpdatedDate = source.UpdatedDate,
            Materials = materials
        };
    }

    private async Task<IReadOnlyDictionary<FormulaPricingPolicyLookupKey, ResolvedFormulaPricingPolicy>>
        LoadPricingPoliciesAsync(IReadOnlyCollection<SourceRow> sources, Guid companyId, string currency, CancellationToken cancellationToken)
    {
        var normalizedCurrency = currency.Trim().ToUpperInvariant();
        var keys = sources
            .Where(x => x.ProductCategoryId != Guid.Empty && x.PricingProfile.HasValue)
            .Select(x => new FormulaPricingPolicyLookupKey(
                companyId, x.ProductCategoryId, x.PricingProfile!.Value, normalizedCurrency))
            .Distinct();
        var policies = await _pricingPolicyResolver.GetPublishedBatchAsync(keys, cancellationToken);
        return policies;
    }

    private static void ApplyLegacyPricingProfiles(IEnumerable<SourceRow> sources)
    {
        foreach (var source in sources)
        {
            source.PricingProfile = FormulaPricingProfileResolver.Resolve(
                source.ProductColourCode,
                source.ProductCode,
                source.ProductAdditive);
        }
    }

    private static QuotationProductPricingMaterialDto MapMaterial(
        MaterialRow material,
        IReadOnlyDictionary<PriceItemKey, LatestItemPriceDto> latestPriceByItem)
    {
        var normalizedType = FormulaRealtimeMaterialCostCalculator.NormalizeItemType(material.ItemType);
        LatestItemPriceDto? latestPrice = null;
        var hasPrice = material.ItemId.HasValue &&
            latestPriceByItem.TryGetValue(
                new PriceItemKey(normalizedType, material.ItemId.Value),
                out latestPrice) &&
            latestPrice.PriceSource != LatestPriceSourceType.Unknown;

        return new QuotationProductPricingMaterialDto
        {
            FormulaMaterialId = material.SourceMaterialId,
            ItemId = material.ItemId,
            ItemType = material.ItemType,
            ItemCode = material.ItemCode,
            ItemName = material.ItemName,
            Quantity = material.Quantity,
            Unit = material.Unit,
            CategoryId = material.CategoryId,
            HasLatestPrice = hasPrice,
            LatestUnitPrice = hasPrice ? latestPrice!.CurrentPrice : null,
            LatestTotalPrice = hasPrice
                ? PricingRoundingRules.RoundCalculatedPrice(
                    material.Quantity * latestPrice!.CurrentPrice)
                : null,
            LatestPriceDate = hasPrice ? latestPrice!.PriceDate : null,
            LatestPriceSource = hasPrice
                ? latestPrice!.PriceSource
                : LatestPriceSourceType.Unknown
        };
    }

    private readonly record struct SourceKey(ProductPricingSourceType SourceType, Guid SourceId);

    private sealed class SourceRow
    {
        public Guid ProductId { get; init; }
        public Guid ProductCategoryId { get; init; }
        public string? ProductColourCode { get; init; }
        public string? ProductCode { get; init; }
        public string? ProductAdditive { get; init; }
        public FormulaPricingProfile? PricingProfile { get; set; }
        public ProductPricingSourceType SourceType { get; init; }
        public Guid SourceId { get; init; }
        public string ExternalId { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public bool IsEligible { get; init; }
        public bool IsCustomerSelected { get; init; }
        public decimal? ManufacturingCost { get; init; }
        public DateTime? UpdatedDate { get; init; }
        public SourceKey Key => new(SourceType, SourceId);
    }

    private sealed class MaterialRow
    {
        public ProductPricingSourceType SourceType { get; init; }
        public Guid SourceId { get; init; }
        public Guid SourceMaterialId { get; init; }
        public Guid? CategoryId { get; init; }
        public Guid? ItemId { get; init; }
        public ItemType ItemType { get; init; }
        public string ItemCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public decimal Quantity { get; init; }
        public string Unit { get; init; } = string.Empty;
        public int LineNo { get; init; }
        public SourceKey SourceKey => new(SourceType, SourceId);
    }
}

internal readonly record struct ProductPricingSourceSelection(
    Guid ProductId,
    ProductPricingSourceType SourceType,
    Guid SourceId);
