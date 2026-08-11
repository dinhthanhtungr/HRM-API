using HRM.Application.Abstractions.Commons.Pricing;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Commons.Authorization.PLM;
using HRM.Application.Commons.Pricing.Dtos;
using HRM.Application.Commons.Pricing.Helpers;
using HRM.Application.Commons.Pricing.Models;
using HRM.Application.Features.PLM.Formulas.Dtos.Commons;
using HRM.Domain.Enums.Formulas;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Formulas.Queries.GetFormulaById;

internal sealed class GetFormulaByIdQueryHandler
    : IRequestHandler<GetFormulaByIdQuery, FormulaInformationDto?>
{
    private readonly IPLMReadDbContext _dbContext;
    private readonly IPLMFieldVisibilityService _fieldVisibility;
    private readonly IMaterialPriceQueryService _materialPriceQueryService;

    public GetFormulaByIdQueryHandler(
        IPLMReadDbContext dbContext,
        IPLMFieldVisibilityService fieldVisibility,
        IMaterialPriceQueryService materialPriceQueryService)
    {
        _dbContext = dbContext;
        _fieldVisibility = fieldVisibility;
        _materialPriceQueryService = materialPriceQueryService;
    }

    public async Task<FormulaInformationDto?> Handle(
        GetFormulaByIdQuery request,
        CancellationToken cancellationToken)
    {
        if (request.FormulaId == Guid.Empty)
        {
            return null;
        }

        var canViewFormulaPrices = _fieldVisibility.CanViewFormulaPrices();

        var formula = await _dbContext.Formulas
            .AsNoTracking()
            .Where(x => x.FormulaId == request.FormulaId && x.IsActive)
            .Select(x => new
            {
                x.FormulaId,
                x.CompanyId,
                x.ExternalId,
                x.Name,
                x.Status,
                x.CheckBy,
                CheckByName = x.CheckByNavigation != null
                    ? x.CheckByNavigation.FullName
                    : string.Empty,
                x.CheckDate,
                x.SentBy,
                SentByName = x.SentByNavigation != null
                    ? x.SentByNavigation.FullName
                    : string.Empty,
                x.SentDate,
                x.EffectiveDate,
                x.ProductionPrice,
                x.PresidentPrice,
                x.ProfitMarginPrice,
                x.IsSelect,
                x.IsActive,
                Note = x.Note ?? string.Empty,
                x.CreatedDate,
                ProductCode = x.Product.ColourCode ?? x.Product.Code ?? string.Empty,
                ProductAdditive = x.Product.Additive
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (formula is null)
        {
            return null;
        }

        var materials = await _dbContext.FormulaMaterials
            .AsNoTracking()
            .Where(x =>
                x.FormulaId == request.FormulaId &&
                x.IsActive &&
                x.Formula.CompanyId == formula.CompanyId)
            .Select(x => new FormulaMaterialInformationDto
            {
                FormulaMaterialId = x.FormulaMaterialId,
                LineNo = x.LineNo,
                ItemId = x.MaterialId ?? x.ProductId ?? Guid.Empty,
                ItemType = x.itemType,
                CategoryId = x.CategoryId,
                Quantity = x.Quantity,
                Price = new LatestPriceSource(),
                PriceTotal = 0m,
                ItemName = IsMaterial(x.itemType)
                    ? (x.Material != null ? x.Material.Name : x.MaterialNameSnapshot)
                    : (x.Product != null ? x.Product.Name : x.MaterialNameSnapshot),
                ItemExternalId = IsMaterial(x.itemType)
                    ? (x.Material != null ? x.Material.ExternalId : x.MaterialExternalIdSnapshot)
                    : (x.Product != null
                        ? x.Product.SampleRequests
                            .Where(sr => sr.IsActive)
                            .OrderByDescending(sr => sr.CreatedDate)
                            .Select(sr => sr.ExternalId)
                            .FirstOrDefault()
                        : x.MaterialExternalIdSnapshot)
            })
            .OrderBy(x => x.LineNo)
            .ToListAsync(cancellationToken);

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
                .LoadLatestItemPriceInfoDictAsync(priceRequests, cancellationToken);

            supplierPricesByMaterial = await LoadSupplierPricesAsync(
                materials,
                formula.CompanyId,
                cancellationToken);

            foreach (var material in materials)
            {
                ApplyRealtimePrice(material, latestPriceByItem, supplierPricesByMaterial);
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

        var pricing = canViewFormulaPrices
            ? FormulaPriceCalculator.Calculate(
                formula.ProductCode,
                formula.ProductAdditive,
                realtimeMaterialCost ?? 0m,
                formula.ProductionPrice,
                formula.PresidentPrice)
            : null;

        return new FormulaInformationDto
        {
            FormulaId = formula.FormulaId,
            ExternalId = formula.ExternalId,
            Name = formula.Name,
            Status = formula.Status,

            CheckBy = formula.CheckBy,
            CheckByName = formula.CheckByName,
            CheckDate = formula.CheckDate,

            SentBy = formula.SentBy,
            SentByName = formula.SentByName,
            SentDate = formula.SentDate,

            TotalPrice = realtimeMaterialCost,
            RealtimeMaterialCost = realtimeMaterialCost,
            IsRealtimeMaterialCostComplete = isRealtimeMaterialCostComplete,
            MissingMaterialPriceCount = missingMaterialPriceCount,
            EffectiveDate = formula.EffectiveDate,
            ProductionPrice = canViewFormulaPrices ? formula.ProductionPrice : null,
            PresidentPrice = canViewFormulaPrices ? formula.PresidentPrice : null,
            ProfitMarginPrice = canViewFormulaPrices ? formula.ProfitMarginPrice : null,
            ManufacturingCost = pricing?.ManufacturingCost,
            StandardSellingPrice = pricing?.StandardSellingPrice,
            ProfitMarginRate = pricing?.ProfitMarginRate,
            Pricing = pricing,

            IsSelect = formula.IsSelect,
            IsActive = formula.IsActive,
            Note = formula.Note,
            CreatedDate = formula.CreatedDate,
            Materials = materials
        };
    }

    private async Task<IReadOnlyDictionary<Guid, IReadOnlyList<FormulaMaterialSupplierPriceDto>>> LoadSupplierPricesAsync(
        IReadOnlyList<FormulaMaterialInformationDto> materials,
        Guid? companyId,
        CancellationToken cancellationToken)
    {
        if (!companyId.HasValue || companyId.Value == Guid.Empty)
        {
            return new Dictionary<Guid, IReadOnlyList<FormulaMaterialSupplierPriceDto>>();
        }

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
                x.Material.CompanyId == companyId.Value &&
                x.Supplier.IsActive == true &&
                x.Supplier.CompanyId == companyId.Value)
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
        FormulaMaterialInformationDto material,
        IReadOnlyDictionary<PriceItemKey, LatestItemPriceDto> latestPriceByItem,
        IReadOnlyDictionary<Guid, IReadOnlyList<FormulaMaterialSupplierPriceDto>> supplierPricesByMaterial)
    {
        var normalizedItemType = NormalizePriceItemType(material.ItemType);
        LatestItemPriceDto? latestPrice = null;
        var hasLatestPrice = material.ItemId != Guid.Empty &&
            latestPriceByItem.TryGetValue(
                new PriceItemKey(normalizedItemType, material.ItemId),
                out latestPrice) &&
            latestPrice.PriceSource != LatestPriceSourceType.Unknown;

        var latestUnitPrice = hasLatestPrice
            ? latestPrice!.CurrentPrice
            : 0m;
        var latestTotalPrice = RoundMoney(material.Quantity * latestUnitPrice);
        var latestPriceDate = hasLatestPrice
            ? latestPrice!.PriceDate
            : null;
        var latestPriceSource = hasLatestPrice
            ? latestPrice!.PriceSource
            : LatestPriceSourceType.Unknown;

        material.HasLatestPrice = hasLatestPrice;
        material.LatestUnitPrice = latestUnitPrice;
        material.LatestTotalPrice = latestTotalPrice;
        material.LatestPriceDate = latestPriceDate;
        material.LatestPriceSource = latestPriceSource;
        material.Price = new LatestPriceSource
        {
            UnitPrice = latestUnitPrice,
            LatestPriceDate = latestPriceDate,
            Source = latestPriceSource
        };
        material.PriceTotal = latestTotalPrice;
        material.SupplierPrices = IsMaterial(material.ItemType) && material.ItemId != Guid.Empty
            ? supplierPricesByMaterial.GetValueOrDefault(material.ItemId) ?? []
            : [];
    }

    private static bool IsMaterial(ItemType itemType)
    {
        return itemType is ItemType.Material or ItemType.MaterialFailure;
    }

    private static ItemType NormalizePriceItemType(ItemType itemType)
    {
        return itemType switch
        {
            ItemType.MaterialFailure => ItemType.Material,
            ItemType.ProductFailure => ItemType.Product,
            _ => itemType
        };
    }

    private static decimal RoundMoney(decimal value)
    {
        return decimal.Round(value, 6, MidpointRounding.AwayFromZero);
    }
}
