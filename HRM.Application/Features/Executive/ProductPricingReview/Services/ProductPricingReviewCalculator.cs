using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Concurrency;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pricing.Helpers;
using HRM.Application.Commons.Pricing.Models;
using HRM.Application.Commons.Pricing.Services;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Application.Features.Executive.ProductPricingReview.Dtos;
using HRM.Application.Features.PLM.Formulas.Helpers;
using HRM.Domain.Enums.CustomerEnum;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Executive.ProductPricingReview.Services;

internal sealed class ProductPricingReviewCalculator
{
    private readonly IPLMReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly ProductPricingRealtimeSourceQueryService _sourceQueryService;
    private readonly FormulaPricingEngine _pricingEngine;

    public ProductPricingReviewCalculator(
        IPLMReadDbContext dbContext,
        ICurrentUser currentUser,
        ProductPricingRealtimeSourceQueryService sourceQueryService,
        FormulaPricingEngine pricingEngine)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _sourceQueryService = sourceQueryService;
        _pricingEngine = pricingEngine;
    }

    public async Task<OperationResult<PricingReviewPreviewDto>> PreviewAsync(
        Guid productId,
        PricingReviewPreviewRequest request,
        CancellationToken cancellationToken)
    {
        var calculation = await CalculateAsync(productId, request, cancellationToken);
        return !calculation.Success || calculation.Data is null
            ? OperationResult<PricingReviewPreviewDto>.Fail(calculation.Message!)
            : OperationResult<PricingReviewPreviewDto>.Ok(calculation.Data.Preview);
    }

    internal async Task<OperationResult<PricingReviewCalculation>> CalculateAsync(
        Guid productId,
        PricingReviewPreviewRequest request,
        CancellationToken cancellationToken)
    {
        var context = ProductPricingReviewRules.GetContext(_currentUser);
        if (!context.Success)
            return OperationResult<PricingReviewCalculation>.Fail(context.Message!);
        if (productId == Guid.Empty || request.SourceId == Guid.Empty)
            return OperationResult<PricingReviewCalculation>.Fail("ProductId and SourceId are required.");
        var currency = ProductPricingReviewRules.NormalizeCurrency(request.Currency);
        if (currency is null)
            return OperationResult<PricingReviewCalculation>.Fail("Only VND is supported.");
        var companyId = context.Data.CompanyId;
        var product = await _dbContext.Products.AsNoTracking()
            .Where(x => x.ProductId == productId && x.CompanyId == companyId && x.IsActive)
            .Select(x => new
            {
                x.ProductId,
                x.CategoryId,
                x.ColourCode,
                x.Code,
                x.Additive
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (product is null)
            return OperationResult<PricingReviewCalculation>.Fail(
                "Product was not found, inactive, or outside the current company.");

        var legacyType = ProductPricingReviewRules.ToLegacy(request.SourceType);
        var sources = await _sourceQueryService.LoadSelectedForExecutiveAsync(
            [new ProductPricingSourceSelection(productId, legacyType, request.SourceId)],
            companyId,
            currency,
            cancellationToken);
        var source = sources.GetValueOrDefault(
            new ProductPricingSourceSelection(productId, legacyType, request.SourceId));
        var isExecutiveEligible = source is not null && (source.IsEligible ||
            (request.SourceType == PricingReviewSourceType.VU
                ? await _dbContext.Formulas.AsNoTracking().AnyAsync(x =>
                    x.FormulaId == request.SourceId &&
                    x.ProductId == productId &&
                    x.CompanyId == companyId &&
                    x.IsActive &&
                    ProductPricingReviewRules.EligibleVuFormulaStatuses.Contains(x.Status),
                    cancellationToken)
                : await _dbContext.ManufacturingFormulas.AsNoTracking().AnyAsync(x =>
                    x.ManufacturingFormulaId == request.SourceId &&
                    x.CompanyId == companyId &&
                    x.IsActive &&
                    (x.ProductStandardFormulas.Any(link =>
                         link.ProductId == productId &&
                         link.CompanyId == companyId &&
                         link.Product.IsActive &&
                         link.Product.CompanyId == companyId) ||
                     x.ProductionSelectVersions.Any(version =>
                         version.CompanyId == companyId &&
                         version.MfgProductionOrder.CompanyId == companyId &&
                         version.MfgProductionOrder.IsActive &&
                         version.MfgProductionOrder.ProductId == productId &&
                         version.MfgProductionOrder.Product.IsActive &&
                         version.MfgProductionOrder.Product.CompanyId == companyId)),
                    cancellationToken)));
        if (source is null || !isExecutiveEligible)
            return OperationResult<PricingReviewCalculation>.Fail(
                "Selected pricing source is not eligible for this product and company.");

        var selections = request.MaterialPriceSelections ?? [];
        if (selections.GroupBy(x => x.FormulaMaterialId).Any(x => x.Count() > 1))
            return OperationResult<PricingReviewCalculation>.Fail(
                "MaterialPriceSelections contains duplicate FormulaMaterialId values.");

        var selectedPrices = new Dictionary<Guid, decimal>();
        if (selections.Count > 0)
        {
            var supplierPriceIds = selections.Select(x => x.SupplierPriceId).Distinct().ToArray();
            var supplierPrices = await _dbContext.MaterialsSuppliers.AsNoTracking()
                .Where(x => supplierPriceIds.Contains(x.MaterialsSuppliersId) &&
                            x.IsActive == true && x.Material.CompanyId == companyId &&
                            x.Supplier.CompanyId == companyId && x.Supplier.IsActive == true)
                .Select(x => new
                {
                    x.MaterialsSuppliersId,
                    x.MaterialId,
                    x.CurrentPrice,
                    Currency = x.Currency ?? "VND",
                    UpdatedAt = x.UpdatedDate ?? x.CreateDate
                })
                .ToDictionaryAsync(x => x.MaterialsSuppliersId, cancellationToken);

            foreach (var selection in selections)
            {
                var material = source.Materials.FirstOrDefault(x =>
                    x.FormulaMaterialId == selection.FormulaMaterialId &&
                    x.ItemId == selection.MaterialId);
                if (material is null)
                    return OperationResult<PricingReviewCalculation>.Fail(
                        "A material selection does not belong to the selected source.");
                if (!supplierPrices.TryGetValue(selection.SupplierPriceId, out var supplierPrice) ||
                    supplierPrice.MaterialId != selection.MaterialId ||
                    !string.Equals(supplierPrice.Currency, currency, StringComparison.OrdinalIgnoreCase))
                    return OperationResult<PricingReviewCalculation>.Fail(
                        "A supplier price does not belong to the selected material/company/currency.");
                if (!supplierPrice.CurrentPrice.HasValue || supplierPrice.CurrentPrice.Value != selection.UnitPrice)
                    return OperationResult<PricingReviewCalculation>.Fail(
                        "A supplier price changed. Reload supplier prices before previewing again.");
                if (OptimisticConcurrencyHelper.ValidateExpectedUpdatedDateWithDatabasePrecision(
                        selection.ExpectedUpdatedAt,
                        supplierPrice.UpdatedAt,
                        "Supplier price") is not null)
                    return OperationResult<PricingReviewCalculation>.Fail(
                        "A supplier price was updated by another user. Reload before continuing.");
                selectedPrices[selection.FormulaMaterialId] = selection.UnitPrice;
            }
        }

        decimal materialCost = 0m;
        var missingPriceCount = 0;
        foreach (var material in source.Materials)
        {
            var unitPrice = selectedPrices.GetValueOrDefault(material.FormulaMaterialId);
            if (!selectedPrices.ContainsKey(material.FormulaMaterialId))
            {
                if (!material.HasLatestPrice || !material.LatestUnitPrice.HasValue)
                {
                    missingPriceCount++;
                    // President may still save or approve a manually chosen selling price.
                    // Preserve the Formula/VA snapshot when present; otherwise the unresolved
                    // item contributes zero while MissingPriceCount remains visible to the UI.
                    unitPrice = material.HasSourcePriceSnapshot && material.SourceUnitPrice.HasValue
                        ? material.SourceUnitPrice.Value
                        : 0m;
                }
                else
                {
                    unitPrice = material.LatestUnitPrice.Value;
                }
            }
            materialCost += material.Quantity * unitPrice;
        }
        materialCost = PricingRoundingRules.RoundCalculatedPrice(materialCost);

        var changedField = ProductPricingReviewRules.ToLegacy(request.ChangedField);
        if (request.ChangedField == PricingReviewChangedField.ProfitMarginPercent &&
            request.ProfitMarginPercent is not (>= 0m and < 100m))
            return OperationResult<PricingReviewCalculation>.Fail(
                "ProfitMarginPercent must be between 0 (inclusive) and 100 (exclusive).");
        var engineResult = await _pricingEngine.ResolveAsync(new PricingEngineRequest
        {
            CompanyId = companyId,
            CategoryId = product.CategoryId,
            ProductId = productId,
            SourceId = request.SourceId,
            SourceType = legacyType.ToString(),
            Profile = FormulaPricingProfileResolver.Resolve(product.ColourCode, product.Code, product.Additive),
            Currency = currency,
            MaterialCost = materialCost,
            ManufacturingCostOverride = request.ManufacturingCost ?? source.ManufacturingCost,
            StandardSellingPrice = request.StandardSellingPrice ?? source.StandardSellingPrice,
            ProfitMarginRate = request.ProfitMarginPercent,
            ChangedField = changedField
        }, cancellationToken);
        if (!engineResult.Success || engineResult.Data?.Calculation is null)
            return OperationResult<PricingReviewCalculation>.Fail(
                engineResult.Message ?? "Pricing calculation is unavailable for the selected source.");

        var result = engineResult.Data;
        var tiers = ResolveTiers(request.PriceTiers, result.SuggestedTiers);
        if (!tiers.Success || tiers.Data is null)
            return OperationResult<PricingReviewCalculation>.Fail(tiers.Message!);
        var versionNumber = await GetSourceVersionNumberAsync(legacyType, request.SourceId, companyId, cancellationToken);
        var costBase = result.CostBase!.Value;
        var standardPrice = result.StandardSellingPrice!.Value;
        var publicSource = new PricingReviewSourceOptionDto
        {
            SourceType = request.SourceType,
            SourceId = source.SourceId,
            SourceCode = source.ExternalId,
            SourceName = source.Name,
            DisplayName = source.ExternalId + " · " + source.Name,
            VersionNumber = versionNumber,
            Status = source.Status,
            IsEligible = isExecutiveEligible,
            IsCurrentlyApplied = source.IsCustomerSelected,
            UpdatedAt = source.UpdatedDate
        };
        var preview = new PricingReviewPreviewDto
        {
            Source = publicSource,
            MaterialCost = result.MaterialCost!.Value,
            ManufacturingCost = result.ManufacturingCost!.Value,
            CostBase = costBase,
            StandardSellingPrice = standardPrice,
            ProfitAmount = standardPrice - costBase,
            ProfitMarginPercent = PricingMarginCalculator.CalculateProfitMarginPercent(
                standardPrice,
                costBase) ?? 0m,
            MissingPriceCount = missingPriceCount,
            PriceTiers = tiers.Data
        };
        return OperationResult<PricingReviewCalculation>.Ok(new PricingReviewCalculation(
            preview,
            result.FormulaPricingPolicyId,
            result.FormulaPricingPolicyVersion,
            legacyType,
            source.ExternalId,
            versionNumber,
            result.ProfitMarginRate,
            result.SuggestedTiers));
    }

    private static OperationResult<IReadOnlyList<PricingReviewPriceTierDto>> ResolveTiers(
        IReadOnlyList<PricingReviewPriceTierRequest>? requested,
        IReadOnlyList<HRM.Application.Commons.Pricing.Dtos.FormulaSuggestedPriceTierDto> suggested)
    {
        requested ??= [];
        var legacy = requested.Select(x => new ProductPricingTierRequest
        {
            QuantityRangeLabel = x.QuantityRangeLabel,
            MinQuantity = x.MinQuantity,
            MaxQuantity = x.MaxQuantity,
            MinInclusive = x.MinInclusive,
            MaxInclusive = x.MaxInclusive,
            UnitPrice = x.UnitPrice,
            SortOrder = x.SortOrder,
            IsActive = x.IsActive
        }).ToArray();
        var validation = ProductPricingVersionPolicyRules.BuildPolicyTiers(
            Guid.Empty,
            suggested,
            legacy);
        if (!validation.Success || validation.Data is null)
            return OperationResult<IReadOnlyList<PricingReviewPriceTierDto>>.Fail(validation.Message!);
        return OperationResult<IReadOnlyList<PricingReviewPriceTierDto>>.Ok(
            validation.Data.Tiers.OrderBy(x => x.SortOrder).Select(x => new PricingReviewPriceTierDto
            {
                QuantityRangeLabel = x.QuantityRangeLabel,
                MinQuantity = x.MinQuantity,
                MaxQuantity = x.MaxQuantity,
                MinInclusive = x.MinInclusive,
                MaxInclusive = x.MaxInclusive,
                UnitPrice = x.UnitPrice,
                SortOrder = x.SortOrder,
                IsActive = x.IsActive
            }).ToArray());
    }

    private Task<int?> GetSourceVersionNumberAsync(
        ProductPricingSourceType type, Guid sourceId, Guid companyId, CancellationToken cancellationToken)
        => type == ProductPricingSourceType.Formula
            ? _dbContext.FormulaVersions.AsNoTracking()
                .Where(x => x.FormulaId == sourceId && x.Formula.CompanyId == companyId)
                .MaxAsync(x => (int?)x.VersionNo, cancellationToken)
            : _dbContext.ManufacturingFormulaVersions.AsNoTracking()
                .Where(x => x.ManufacturingFormulaId == sourceId && x.ManufacturingFormula.CompanyId == companyId)
                .MaxAsync(x => (int?)x.VersionNo, cancellationToken);
}

internal sealed record PricingReviewCalculation(
    PricingReviewPreviewDto Preview,
    Guid FormulaPricingPolicyId,
    int FormulaPricingPolicyVersion,
    ProductPricingSourceType SourceType,
    string SourceExternalId,
    int? SourceVersionNumber,
    decimal? ProfitMarginRate,
    IReadOnlyList<HRM.Application.Commons.Pricing.Dtos.FormulaSuggestedPriceTierDto> SuggestedTiers);
