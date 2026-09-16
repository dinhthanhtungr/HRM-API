using HRM.Application.Abstractions.Commons.Pricing;
using HRM.Application.Abstractions.Persistence.Commons.Pricing;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.PLM.Shared.Authorization;
using HRM.Application.Commons.Pricing.Dtos;
using HRM.Application.Commons.Pricing.Helpers;
using HRM.Application.Commons.Pricing.Models;
using HRM.Application.Commons.Pricing.Services;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Application.Features.PLM.Formulas.Dtos.Commons;
using HRM.Application.Features.PLM.Formulas.Helpers;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.Formulas;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Formulas.Queries.GetManufacturingFormulaMaterials;

internal sealed class GetManufacturingFormulaMaterialsQueryHandler
    : IRequestHandler<GetManufacturingFormulaMaterialsQuery, FormulaInformationDto?>
{
    private const string Currency = "VND";

    private readonly IPLMReadDbContext _dbContext;
    private readonly IPriceReadDbContext _priceDbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IPLMFieldVisibilityService _fieldVisibility;
    private readonly IMaterialPriceQueryService _materialPriceQueryService;
    private readonly FormulaPricingEngine _pricingEngine;

    public GetManufacturingFormulaMaterialsQueryHandler(
        IPLMReadDbContext dbContext,
        IPriceReadDbContext priceDbContext,
        ICurrentUser currentUser,
        IPLMFieldVisibilityService fieldVisibility,
        IMaterialPriceQueryService materialPriceQueryService,
        FormulaPricingEngine pricingEngine)
    {
        _dbContext = dbContext;
        _priceDbContext = priceDbContext;
        _currentUser = currentUser;
        _fieldVisibility = fieldVisibility;
        _materialPriceQueryService = materialPriceQueryService;
        _pricingEngine = pricingEngine;
    }

    public async Task<FormulaInformationDto?> Handle(
        GetManufacturingFormulaMaterialsQuery request,
        CancellationToken cancellationToken)
    {
        if (request.ManufacturingFormulaId == Guid.Empty ||
            _currentUser.CompanyId is not { } companyId ||
            companyId == Guid.Empty)
        {
            return null;
        }

        var canViewFormulaPrices = _fieldVisibility.CanViewFormulaPrices();
        var formula = await _dbContext.ManufacturingFormulas
            .AsNoTracking()
            .Where(x =>
                x.ManufacturingFormulaId == request.ManufacturingFormulaId &&
                x.CompanyId == companyId &&
                x.IsActive)
            .Select(x => new
            {
                FormulaId = x.ManufacturingFormulaId,
                x.ExternalId,
                x.Name,
                x.Status,
                x.Note,
                x.IsActive,
                x.CreatedDate,
                x.UpdatedDate,
                SourceVuProductId = x.SourceVUFormula != null
                    ? (Guid?)x.SourceVUFormula.ProductId
                    : null,
                SourceBomProductId = x.SourceBomVersion != null
                    ? (Guid?)x.SourceBomVersion.BomDefinition.ProductId
                    : null,
                EffectiveDate = x.ManufacturingFormulaVersions
                    .Where(version => version.EffectiveTo == null)
                    .OrderByDescending(version => version.EffectiveFrom)
                    .Select(version => version.EffectiveFrom)
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (formula is null)
        {
            return null;
        }

        // Một Manufacturing Formula có thể được dùng cho nhiều lệnh. Ưu tiên lựa chọn
        // hiện hành, sau đó lấy lần dùng gần nhất để response luôn tất định.
        var productionContext = await _dbContext.ProductionSelectVersions
            .AsNoTracking()
            .Where(x =>
                x.ManufacturingFormulaId == request.ManufacturingFormulaId &&
                x.CompanyId == companyId &&
                x.MfgProductionOrder.CompanyId == companyId &&
                x.MfgProductionOrder.IsActive)
            .OrderByDescending(x => x.ValidTo == null && x.ValidFrom != null)
            .ThenByDescending(x => x.ValidFrom)
            .ThenByDescending(x => x.MfgProductionOrder.UpdatedDate)
            .ThenByDescending(x => x.MfgProductionOrderId)
            .Select(x => new
            {
                x.MfgProductionOrder.ProductId,
                x.MfgProductionOrder.StepOfProduct,
                IsCurrent = x.ValidTo == null && x.ValidFrom != null
            })
            .FirstOrDefaultAsync(cancellationToken);

        var productId = productionContext?.ProductId ??
            formula.SourceBomProductId ??
            formula.SourceVuProductId;
        var product = productId.HasValue
            ? await _dbContext.Products
                .AsNoTracking()
                .Where(x =>
                    x.ProductId == productId.Value &&
                    x.CompanyId == companyId &&
                    x.IsActive)
                .Select(x => new
                {
                    x.ProductId,
                    x.CategoryId,
                    x.ColourCode,
                    x.Code,
                    x.Additive
                })
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        var approvedPricing = canViewFormulaPrices && product is not null
            ? await _priceDbContext.ProductPricingVersions
                .AsNoTracking()
                .Where(x =>
                    x.CompanyId == companyId &&
                    x.ProductId == product.ProductId &&
                    x.Currency == Currency &&
                    x.IsActive &&
                    x.Status == ProductPricingStatus.Approved)
                .OrderByDescending(x => x.Version)
                .ThenByDescending(x => x.ApprovedAt ?? x.UpdatedDate ?? x.CreatedDate)
                .Select(x => new
                {
                    x.ManufacturingCost,
                    x.StandardSellingPrice,
                    x.ProfitMarginRate
                })
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        var materials = await _dbContext.ManufacturingFormulaMaterials
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                x.ManufacturingFormulaId == request.ManufacturingFormulaId &&
                x.ManufacturingFormula.CompanyId == companyId)
            .OrderBy(x => x.LineNo)
            .Select(x => new FormulaMaterialInformationDto
            {
                FormulaMaterialId = x.ManufacturingFormulaMaterialId,
                LineNo = x.LineNo,
                ItemId = x.MaterialId ?? x.ProductId ?? Guid.Empty,
                ItemType = x.itemType,
                CategoryId = x.CategoryId,
                Quantity = x.Quantity,
                ItemName = x.MaterialNameSnapshot,
                ItemExternalId = x.MaterialExternalIdSnapshot
            })
            .ToListAsync(cancellationToken);

        var currentItemData = await FormulaItemDisplayResolver.LoadCurrentDataAsync(
            _dbContext,
            companyId,
            materials.Select(item => new FormulaItemDisplaySource(
                item.ItemId,
                item.ItemType,
                item.ItemName,
                item.ItemExternalId)),
            cancellationToken);

        foreach (var item in materials)
        {
            var display = FormulaItemDisplayResolver.Resolve(
                new FormulaItemDisplaySource(
                    item.ItemId,
                    item.ItemType,
                    item.ItemName,
                    item.ItemExternalId),
                currentItemData);
            item.ItemName = display.Name;
            item.ItemExternalId = display.ExternalId;
        }

        IReadOnlyDictionary<Guid, IReadOnlyList<FormulaMaterialSupplierPriceDto>> supplierPricesByMaterial =
            new Dictionary<Guid, IReadOnlyList<FormulaMaterialSupplierPriceDto>>();

        if (canViewFormulaPrices)
        {
            var priceRequests = materials
                .Where(x => x.ItemId != Guid.Empty)
                .Select(x => new PriceItemRequest
                {
                    ItemType = NormalizePriceItemType(x.ItemType),
                    MaterialId = IsMaterial(x.ItemType) ? x.ItemId : null,
                    ProductId = IsMaterial(x.ItemType) ? null : x.ItemId
                })
                .ToList();

            var latestPriceByItem = await _materialPriceQueryService
                .LoadLatestPricingItemPriceInfoDictAsync(
                    companyId,
                    Currency,
                    priceRequests,
                    cancellationToken);

            supplierPricesByMaterial = await LoadSupplierPricesAsync(
                materials,
                companyId,
                cancellationToken);

            foreach (var item in materials)
            {
                ApplyRealtimePrice(item, latestPriceByItem, supplierPricesByMaterial);
            }
        }

        var realtimeMaterialCost = canViewFormulaPrices
            ? RoundMoney(materials.Sum(x => x.LatestTotalPrice ?? 0m))
            : (decimal?)null;
        var missingMaterialPriceCount = canViewFormulaPrices
            ? materials.Count(x => !x.HasLatestPrice)
            : (int?)null;
        var isRealtimeMaterialCostComplete = canViewFormulaPrices
            ? materials.Count > 0 && missingMaterialPriceCount == 0
            : (bool?)null;

        var pricingProfile = product is null
            ? FormulaPricingProfile.Powder
            : FormulaPricingProfileResolver.Resolve(
                product.ColourCode,
                product.Code,
                product.Additive);
        var engineResult = canViewFormulaPrices && product is not null
            ? await _pricingEngine.ResolveAsync(
                new PricingEngineRequest
                {
                    CompanyId = companyId,
                    CategoryId = product.CategoryId,
                    ProductId = product.ProductId,
                    SourceId = formula.FormulaId,
                    SourceType = "ManufacturingFormula",
                    Profile = pricingProfile,
                    Currency = Currency,
                    MaterialCost = isRealtimeMaterialCostComplete == true
                        ? realtimeMaterialCost
                        : null,
                    MaterialItems = materials.Select(item => new FormulaMaterialCostItem(
                        item.ItemId == Guid.Empty ? null : item.ItemId,
                        item.ItemType,
                        item.Quantity)).ToArray()
                },
                cancellationToken)
            : null;
        var enginePricing = engineResult is { Success: true } ? engineResult.Data : null;
        var pricingStatus = !canViewFormulaPrices
            ? "Hidden"
            : product is null
                ? "ProductContextMissing"
                : engineResult is { Success: false }
                    ? engineResult.Message ?? FormulaPricingPolicyRules.PricingPolicyMissing
                    : enginePricing?.IsMaterialCostComplete == true
                        ? "Available"
                        : "MaterialPriceMissing";

        return new FormulaInformationDto
        {
            FormulaId = formula.FormulaId,
            ExternalId = formula.ExternalId,
            Name = formula.Name,
            Status = formula.Status,
            StepOfProduct = productionContext?.StepOfProduct,
            TotalPrice = realtimeMaterialCost,
            RealtimeMaterialCost = realtimeMaterialCost,
            IsRealtimeMaterialCostComplete = isRealtimeMaterialCostComplete,
            MissingMaterialPriceCount = missingMaterialPriceCount,
            EffectiveDate = formula.EffectiveDate,
            ProductionPrice = canViewFormulaPrices ? approvedPricing?.ManufacturingCost : null,
            ManufacturingCost = canViewFormulaPrices ? approvedPricing?.ManufacturingCost : null,
            StandardSellingPrice = canViewFormulaPrices ? approvedPricing?.StandardSellingPrice : null,
            ProfitMarginRate = canViewFormulaPrices ? approvedPricing?.ProfitMarginRate : null,
            PricingStatus = pricingStatus,
            FormulaPricingPolicyId = enginePricing?.FormulaPricingPolicyId,
            FormulaPricingPolicyVersion = enginePricing?.FormulaPricingPolicyVersion,
            SuggestedPriceTiers = enginePricing?.SuggestedTiers ?? [],
            Pricing = enginePricing?.Calculation,
            IsSelect = productionContext?.IsCurrent == true,
            IsActive = formula.IsActive,
            Note = formula.Note,
            CreatedDate = formula.CreatedDate,
            UpdatedDate = formula.UpdatedDate,
            Materials = materials
        };
    }

    private async Task<IReadOnlyDictionary<Guid, IReadOnlyList<FormulaMaterialSupplierPriceDto>>> LoadSupplierPricesAsync(
        IReadOnlyList<FormulaMaterialInformationDto> materials,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var materialIds = materials
            .Where(x => IsMaterial(x.ItemType) && x.ItemId != Guid.Empty)
            .Select(x => x.ItemId)
            .Distinct()
            .ToList();

        if (materialIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<FormulaMaterialSupplierPriceDto>>();
        }

        var supplierRows = await _dbContext.MaterialsSuppliers
            .AsNoTracking()
            .Where(x =>
                x.IsActive == true &&
                materialIds.Contains(x.MaterialId) &&
                x.Material.IsActive == true &&
                x.Material.CompanyId == companyId &&
                x.Supplier.IsActive == true &&
                x.Supplier.CompanyId == companyId)
            .OrderByDescending(x => x.IsPreferred)
            .ThenBy(x => x.Supplier.SupplierName)
            .Select(x => new
            {
                x.MaterialId,
                MaterialsSupplierId = x.MaterialsSuppliersId,
                x.SupplierId,
                SupplierCode = x.Supplier.ExternalId ?? string.Empty,
                SupplierName = x.Supplier.SupplierName ?? string.Empty,
                x.CurrentPrice,
                x.Currency,
                IsPreferred = x.IsPreferred == true,
                x.UpdatedDate
            })
            .ToListAsync(cancellationToken);

        return supplierRows
            .GroupBy(x => x.MaterialId)
            .ToDictionary(
                x => x.Key,
                x => (IReadOnlyList<FormulaMaterialSupplierPriceDto>)x
                    .Select(y => new FormulaMaterialSupplierPriceDto
                    {
                        MaterialsSupplierId = y.MaterialsSupplierId,
                        SupplierId = y.SupplierId,
                        SupplierCode = y.SupplierCode,
                        SupplierName = y.SupplierName,
                        CurrentPrice = y.CurrentPrice,
                        Currency = y.Currency,
                        IsPreferred = y.IsPreferred,
                        UpdatedDate = y.UpdatedDate
                    })
                    .ToList());
    }

    private static void ApplyRealtimePrice(
        FormulaMaterialInformationDto item,
        IReadOnlyDictionary<PriceItemKey, LatestItemPriceDto> latestPriceByItem,
        IReadOnlyDictionary<Guid, IReadOnlyList<FormulaMaterialSupplierPriceDto>> supplierPricesByMaterial)
    {
        var normalizedItemType = NormalizePriceItemType(item.ItemType);
        LatestItemPriceDto? latestPrice = null;
        var hasLatestPrice = item.ItemId != Guid.Empty &&
            latestPriceByItem.TryGetValue(
                new PriceItemKey(normalizedItemType, item.ItemId),
                out latestPrice) &&
            latestPrice.PriceSource != LatestPriceSourceType.Unknown;
        var latestUnitPrice = hasLatestPrice ? latestPrice!.CurrentPrice : 0m;
        var latestTotalPrice = RoundMoney(item.Quantity * latestUnitPrice);
        var latestPriceDate = hasLatestPrice ? latestPrice!.PriceDate : null;
        var latestPriceSource = hasLatestPrice
            ? latestPrice!.PriceSource
            : LatestPriceSourceType.Unknown;

        item.HasLatestPrice = hasLatestPrice;
        item.LatestUnitPrice = latestUnitPrice;
        item.LatestTotalPrice = latestTotalPrice;
        item.LatestPriceDate = latestPriceDate;
        item.LatestPriceSource = latestPriceSource;
        item.Price = new LatestPriceSource
        {
            UnitPrice = latestUnitPrice,
            LatestPriceDate = latestPriceDate,
            Source = latestPriceSource,
            Calculation = hasLatestPrice ? latestPrice?.Calculation : null
        };
        item.PriceTotal = latestTotalPrice;
        item.SupplierPrices = IsMaterial(item.ItemType) && item.ItemId != Guid.Empty
            ? supplierPricesByMaterial.GetValueOrDefault(item.ItemId) ?? []
            : [];
    }

    private static bool IsMaterial(ItemType itemType) =>
        itemType is ItemType.Material or ItemType.MaterialFailure;

    private static ItemType NormalizePriceItemType(ItemType itemType) => itemType switch
    {
        ItemType.MaterialFailure => ItemType.Material,
        ItemType.ProductFailure => ItemType.Product,
        _ => itemType
    };

    private static decimal RoundMoney(decimal value) =>
        decimal.Round(value, 6, MidpointRounding.AwayFromZero);
}
