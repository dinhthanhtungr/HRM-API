using HRM.Application.Abstractions.Commons.Pricing;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Commons.Pricing.Dtos;
using HRM.Application.Commons.Pricing.Helpers;
using HRM.Application.Commons.Pricing.Models;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.Formulas;
using HRM.Domain.Enums.SampleRequests;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Services;

/// <summary>
/// Tính giá hiện tại cho nhiều sản phẩm bằng cùng rule và một lần tải giá nguyên vật liệu.
/// </summary>
internal sealed class QuotationCurrentPricingResolver
{
    private readonly IPLMReadDbContext _dbContext;
    private readonly IMaterialPriceQueryService _materialPriceQueryService;

    public QuotationCurrentPricingResolver(
        IPLMReadDbContext dbContext,
        IMaterialPriceQueryService materialPriceQueryService)
    {
        _dbContext = dbContext;
        _materialPriceQueryService = materialPriceQueryService;
    }

    public async Task<IReadOnlyDictionary<Guid, QuotationCurrentProductPricing>> ResolveAsync(
        IEnumerable<Guid> requestedProductIds,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var productIds = requestedProductIds
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToArray();
        if (productIds.Length == 0)
        {
            return new Dictionary<Guid, QuotationCurrentProductPricing>();
        }

        var products = await _dbContext.Products
            .AsNoTracking()
            .Where(x =>
                productIds.Contains(x.ProductId) &&
                x.CompanyId == companyId &&
                x.IsActive)
            .Select(x => new PricingProduct(
                x.ProductId,
                x.ColourCode ?? x.Code ?? string.Empty,
                x.Name ?? string.Empty,
                x.Additive))
            .ToListAsync(cancellationToken);

        var selectedFormulaRows = await _dbContext.Formulas
            .AsNoTracking()
            .Where(x =>
                productIds.Contains(x.ProductId) &&
                x.CompanyId == companyId &&
                x.IsActive &&
                x.IsSelect)
            .OrderByDescending(x => x.UpdatedDate ?? x.CreatedDate)
            .ThenByDescending(x => x.FormulaId)
            .Select(x => new PricingFormula(
                x.ProductId,
                x.FormulaId,
                x.ExternalId,
                x.Name,
                x.ProductionPrice,
                x.PresidentPrice,
                x.UpdatedDate,
                QuotationFormulaSelectionSource.CustomerSelected))
            .ToListAsync(cancellationToken);

        var formulaByProductId = selectedFormulaRows
            .GroupBy(x => x.ProductId)
            .ToDictionary(x => x.Key, x => x.First());
        var productsWithoutSelectedFormula = productIds
            .Where(x => !formulaByProductId.ContainsKey(x))
            .ToArray();

        if (productsWithoutSelectedFormula.Length > 0)
        {
            var sampleSentStatus = SampleRequestStatus.SampleSent.ToString();
            var completedStatus = SampleRequestStatus.Completed.ToString();
            var fallbackFormulaRows = await _dbContext.SampleRequests
                .AsNoTracking()
                .Where(x =>
                    productsWithoutSelectedFormula.Contains(x.ProductId) &&
                    x.CompanyId == companyId &&
                    x.IsActive &&
                    x.FormulaId.HasValue &&
                    x.Formula != null &&
                    x.Formula.IsActive &&
                    x.Formula.CompanyId == companyId &&
                    x.Formula.ProductId == x.ProductId &&
                    (x.Status == sampleSentStatus || x.Status == completedStatus))
                .OrderByDescending(x => x.SendDate ?? x.UpdatedDate ?? x.CreatedDate)
                .ThenByDescending(x => x.SampleRequestId)
                .Select(x => new PricingFormula(
                    x.ProductId,
                    x.FormulaId!.Value,
                    x.Formula!.ExternalId,
                    x.Formula.Name,
                    x.Formula.ProductionPrice,
                    x.Formula.PresidentPrice,
                    x.Formula.UpdatedDate,
                    QuotationFormulaSelectionSource.LatestSampleRequest))
                .ToListAsync(cancellationToken);

            foreach (var formula in fallbackFormulaRows
                         .GroupBy(x => x.ProductId)
                         .Select(x => x.First()))
            {
                formulaByProductId[formula.ProductId] = formula;
            }
        }

        var formulaIds = formulaByProductId.Values
            .Select(x => x.FormulaId)
            .Distinct()
            .ToArray();
        var materialRows = formulaIds.Length == 0
            ? []
            : await _dbContext.FormulaMaterials
                .AsNoTracking()
                .Where(x =>
                    formulaIds.Contains(x.FormulaId) &&
                    x.IsActive &&
                    x.Formula.IsActive &&
                    x.Formula.CompanyId == companyId)
                .Select(x => new PricingMaterial(
                    x.FormulaId,
                    x.itemType == ItemType.Material ||
                    x.itemType == ItemType.MaterialFailure
                        ? x.Material != null && x.Material.CompanyId == companyId
                            ? x.MaterialId
                            : null
                        : x.Product != null && x.Product.CompanyId == companyId
                            ? x.ProductId
                            : null,
                    x.itemType,
                    x.Quantity))
                .ToListAsync(cancellationToken);
        var materialsByFormulaId = materialRows
            .GroupBy(x => x.FormulaId)
            .ToDictionary(x => x.Key, x => x.ToArray());

        var priceRequests = materialRows
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
        var latestPriceByItem = await _materialPriceQueryService
            .LoadLatestItemPriceInfoDictAsync(priceRequests, cancellationToken);

        return products.ToDictionary(
            product => product.ProductId,
            product => ResolveProductPricing(
                product,
                formulaByProductId.GetValueOrDefault(product.ProductId),
                materialsByFormulaId,
                latestPriceByItem));
    }

    private static QuotationCurrentProductPricing ResolveProductPricing(
        PricingProduct product,
        PricingFormula? formula,
        IReadOnlyDictionary<Guid, PricingMaterial[]> materialsByFormulaId,
        IReadOnlyDictionary<PriceItemKey, LatestItemPriceDto> latestPriceByItem)
    {
        if (formula is null)
        {
            return new QuotationCurrentProductPricing(
                product.ProductId,
                product.ProductCode,
                product.ProductName,
                null,
                null,
                null,
                null,
                null,
                new FormulaRealtimeMaterialCostResult(null, false, 0),
                null,
                null,
                null,
                false);
        }

        var hasFormulaMaterials = materialsByFormulaId.TryGetValue(
            formula.FormulaId,
            out var materials);
        materials ??= [];
        var realtimeMaterialCost = FormulaRealtimeMaterialCostCalculator.Calculate(
            materials.Select(x => new FormulaMaterialCostItem(
                x.ItemId,
                x.ItemType,
                x.Quantity)),
            latestPriceByItem);
        var pricing = realtimeMaterialCost.IsComplete &&
                      realtimeMaterialCost.MaterialCost.HasValue
            ? FormulaPriceCalculator.Calculate(
                product.ProductCode,
                product.ProductAdditive,
                realtimeMaterialCost.MaterialCost.Value,
                formula.ManufacturingCost,
                formula.StandardSellingPrice)
            : null;
        var manufacturingCost = pricing?.ManufacturingCost ??
            FormulaPriceCalculator.ResolveManufacturingCost(
                product.ProductCode,
                product.ProductAdditive,
                formula.ManufacturingCost);
        var standardSellingPrice =
            pricing?.StandardSellingPrice ?? formula.StandardSellingPrice;

        return new QuotationCurrentProductPricing(
            product.ProductId,
            product.ProductCode,
            product.ProductName,
            formula.FormulaId,
            formula.FormulaExternalId,
            formula.FormulaName,
            formula.SelectionSource,
            formula.PricingUpdatedDate,
            realtimeMaterialCost,
            manufacturingCost,
            standardSellingPrice,
            pricing,
            hasFormulaMaterials);
    }

    private sealed record PricingProduct(
        Guid ProductId,
        string ProductCode,
        string ProductName,
        string? ProductAdditive);

    private sealed record PricingFormula(
        Guid ProductId,
        Guid FormulaId,
        string FormulaExternalId,
        string FormulaName,
        decimal? ManufacturingCost,
        decimal? StandardSellingPrice,
        DateTime? PricingUpdatedDate,
        QuotationFormulaSelectionSource SelectionSource);

    private sealed record PricingMaterial(
        Guid FormulaId,
        Guid? ItemId,
        ItemType ItemType,
        decimal Quantity);
}

internal sealed record QuotationCurrentProductPricing(
    Guid ProductId,
    string ProductCode,
    string ProductName,
    Guid? FormulaId,
    string? FormulaExternalId,
    string? FormulaName,
    QuotationFormulaSelectionSource? FormulaSelectionSource,
    DateTime? PricingUpdatedDate,
    FormulaRealtimeMaterialCostResult RealtimeMaterialCost,
    decimal? ManufacturingCost,
    decimal? StandardSellingPrice,
    FormulaPriceCalculationDto? Pricing,
    bool HasFormulaMaterials);
