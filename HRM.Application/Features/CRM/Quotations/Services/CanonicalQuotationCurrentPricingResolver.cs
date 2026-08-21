using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Commons.Pricing.Dtos;
using HRM.Application.Commons.Pricing.Models;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Domain.Enums.CustomerEnum;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Services;

/// <summary>
/// Adapts the canonical realtime source calculation to quotation read contracts.
/// </summary>
internal sealed class QuotationCurrentPricingResolver
{
    private readonly IPLMReadDbContext _dbContext;
    private readonly ProductPricingRealtimeSourceQueryService _sourceQueryService;

    public QuotationCurrentPricingResolver(
        IPLMReadDbContext dbContext,
        ProductPricingRealtimeSourceQueryService sourceQueryService)
    {
        _dbContext = dbContext;
        _sourceQueryService = sourceQueryService;
    }

    public async Task<IReadOnlyDictionary<Guid, QuotationCurrentProductPricing>> ResolveAsync(
        IEnumerable<Guid> requestedProductIds,
        Guid companyId,
        string currency,
        CancellationToken cancellationToken)
    {
        var productIds = requestedProductIds.Where(x => x != Guid.Empty).Distinct().ToArray();
        if (productIds.Length == 0)
        {
            return new Dictionary<Guid, QuotationCurrentProductPricing>();
        }

        var products = await _dbContext.Products
            .AsNoTracking()
            .Where(x => productIds.Contains(x.ProductId) &&
                        x.CompanyId == companyId &&
                        x.IsActive)
            .Select(x => new
            {
                x.ProductId,
                ProductCode = x.ColourCode ?? x.Code ?? string.Empty,
                ProductName = x.Name ?? string.Empty
            })
            .ToListAsync(cancellationToken);
        var sourcesByProduct = await _sourceQueryService.LoadAsync(
            productIds,
            companyId,
            currency,
            cancellationToken);

        return products.ToDictionary(
            product => product.ProductId,
            product => Map(
                product.ProductId,
                product.ProductCode,
                product.ProductName,
                ProductPricingWorkbenchSourceSelector.ChooseFallback(
                    sourcesByProduct.GetValueOrDefault(product.ProductId) ?? [])));
    }

    private static QuotationCurrentProductPricing Map(
        Guid productId,
        string productCode,
        string productName,
        ProductPricingSourceOptionDto? source)
    {
        if (source is null)
        {
            return new QuotationCurrentProductPricing(
                productId, productCode, productName,
                null, null, null, null, null,
                new FormulaRealtimeMaterialCostResult(null, false, 0),
                null, null, null, false,
                "FormulaNotFound");
        }

        return new QuotationCurrentProductPricing(
            productId,
            productCode,
            productName,
            source.SourceType == ProductPricingSourceType.Formula ? source.SourceId : null,
            source.ExternalId,
            source.Name,
            source.IsCustomerSelected
                ? QuotationFormulaSelectionSource.CustomerSelected
                : QuotationFormulaSelectionSource.LatestSampleRequest,
            source.UpdatedDate,
            new FormulaRealtimeMaterialCostResult(
                source.CurrentMaterialCost,
                source.IsCurrentMaterialCostComplete,
                source.MissingMaterialPriceCount),
            source.ManufacturingCost,
            source.StandardSellingPrice,
            source.Pricing,
            source.Materials.Count > 0,
            source.PricingStatus);
    }
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
    bool HasFormulaMaterials,
    string PricingStatus);
