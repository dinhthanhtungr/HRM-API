using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Commons.Pricing;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Commons.Pricing.Helpers;
using HRM.Application.Commons.Pricing.Models;
using HRM.Application.Commons.Pricing.Services;
using HRM.Application.Commons.Searching;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Application.Features.Executive.ProductPricingReview.Dtos;
using HRM.Application.Features.Pricing.Authorization;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.Manufacturings;
using HRM.Domain.Enums.Products;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Executive.ProductPricingReview.Services;

/// <summary>
/// Read model riêng cho drawer duyệt giá của President. Public contract không phụ thuộc DTO pricing legacy.
/// </summary>
internal sealed class ProductPricingReviewReader
{
    private readonly ICRMReadDbContext _crm;
    private readonly IPLMReadDbContext _plm;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _clock;
    private readonly FormulaPricingEngine _pricingEngine;
    private readonly IFormulaPricingPolicyResolver _pricingPolicyResolver;
    private readonly ProductPricingRealtimeSourceQueryService _realtimeSources;
    private readonly QuotationFeatureOptions _featureOptions;
    private readonly IPricingVisibilityService _pricingVisibilityService;
    private readonly StandardPriceRealtimeComparisonQueryService _comparisonQueryService;
    private readonly ProductPricingRequestQueryService _requestQueryService;

    public ProductPricingReviewReader(
        ICRMReadDbContext crm,
        IPLMReadDbContext plm,
        ICurrentUser currentUser,
        IDateTimeProvider clock,
        FormulaPricingEngine pricingEngine,
        IFormulaPricingPolicyResolver pricingPolicyResolver,
        ProductPricingRealtimeSourceQueryService realtimeSources,
        QuotationFeatureOptions featureOptions,
        IPricingVisibilityService pricingVisibilityService,
        StandardPriceRealtimeComparisonQueryService comparisonQueryService,
        ProductPricingRequestQueryService requestQueryService)
    {
        _crm = crm;
        _plm = plm;
        _currentUser = currentUser;
        _clock = clock;
        _pricingEngine = pricingEngine;
        _pricingPolicyResolver = pricingPolicyResolver;
        _realtimeSources = realtimeSources;
        _featureOptions = featureOptions;
        _pricingVisibilityService = pricingVisibilityService;
        _comparisonQueryService = comparisonQueryService;
        _requestQueryService = requestQueryService;
    }

    /// <summary>
    /// Lấy thông tin review giá của sản phẩm, bao gồm thông tin sản phẩm, 
    /// thông tin khách hàng (nếu có), 
    /// thông tin nguồn giá được chọn, 
    /// thông tin phiên bản giá hiện tại, danh sách nguyên liệu và các số liệu tổng quan.
    /// </summary>
    /// <param name="productId"></param>
    /// <param name="currency"></param>
    /// <param name="quotationId"></param>
    /// <param name="sourceType"></param>
    /// <param name="sourceId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<OperationResult<ProductPricingReviewDto>> GetReviewAsync(
        Guid productId,
        string? currency,
        Guid? quotationId,
        PricingReviewSourceType? sourceType,
        Guid? sourceId,
        CancellationToken cancellationToken)
    {
        var context = ProductPricingReviewRules.GetContext(_currentUser);
        if (!context.Success)
            return OperationResult<ProductPricingReviewDto>.Fail(context.Message!);
        if (productId == Guid.Empty)
            return OperationResult<ProductPricingReviewDto>.Fail("ProductId is required.");

        var normalizedCurrency = ProductPricingReviewRules.NormalizeCurrency(currency);
        if (normalizedCurrency is null)
            return OperationResult<ProductPricingReviewDto>.Fail("Only VND is supported.");
        if (sourceType.HasValue != sourceId.HasValue || sourceId == Guid.Empty)
            return OperationResult<ProductPricingReviewDto>.Fail(
                "SourceType and SourceId must be supplied together.");

        var companyId = context.Data.CompanyId;
        var product = await _plm.Products.AsNoTracking()
            .Where(x => x.ProductId == productId && x.CompanyId == companyId && x.IsActive)
            .Select(x => new
            {
                x.ProductId,
                x.CategoryId,
                Code = x.ColourCode ?? x.Code ?? string.Empty,
                Name = x.Name ?? string.Empty
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (product is null)
            return OperationResult<ProductPricingReviewDto>.Fail(
                "Product was not found, inactive, or outside the current company.");

        var currentVersions = await _crm.ProductPricingVersions.AsNoTracking()
            .Include(x => x.PriceTiers)
            .Where(x => x.CompanyId == companyId && x.ProductId == productId &&
                        x.Currency == normalizedCurrency && x.IsActive &&
                        (x.Status == ProductPricingStatus.Draft || x.Status == ProductPricingStatus.Approved))
            .ToListAsync(cancellationToken);
        var currentVersion = currentVersions
            .OrderBy(x => x.Status == ProductPricingStatus.Draft ? 0 : 1)
            .ThenByDescending(x => x.Version)
            .FirstOrDefault();
        var approvedStandardPricing = currentVersions
            .Where(x => x.Status == ProductPricingStatus.Approved && x.StandardSellingPrice > 0m)
            .OrderByDescending(x => x.Version)
            .ThenByDescending(x => x.ApprovedAt ?? x.UpdatedDate ?? x.CreatedDate)
            .FirstOrDefault();

        ProductPricingSourceOptionDto? selected = null;
        var selectedIsEligible = false;
        if (sourceType.HasValue && sourceId.HasValue)
        {
            var legacyType = ProductPricingReviewRules.ToLegacy(sourceType.Value);
            var selection = new ProductPricingSourceSelection(productId, legacyType, sourceId.Value);
            var selectedSources = await _realtimeSources.LoadSelectedForExecutiveAsync(
                [selection], companyId, normalizedCurrency, cancellationToken);
            selected = selectedSources.GetValueOrDefault(selection);
            selectedIsEligible = selected is not null && await IsExecutiveSourceEligibleAsync(
                productId, companyId, sourceType.Value, sourceId.Value, selected.IsEligible, cancellationToken);
            if (selected is null || !selectedIsEligible)
                return OperationResult<ProductPricingReviewDto>.Fail(
                    "Selected pricing source is not eligible for this product and company.");
        }
        else if (currentVersion is not null)
        {
            var currentSourceId = currentVersion.SourceManufacturingFormulaId ?? currentVersion.SourceFormulaId;
            if (currentSourceId.HasValue)
            {
                var currentSourceType = currentVersion.SourceManufacturingFormulaId.HasValue
                    ? ProductPricingSourceType.ManufacturingFormula
                    : ProductPricingSourceType.Formula;
                var selection = new ProductPricingSourceSelection(
                    productId, currentSourceType, currentSourceId.Value);
                var selectedSources = await _realtimeSources.LoadSelectedForExecutiveAsync(
                    [selection], companyId, normalizedCurrency, cancellationToken);
                selected = selectedSources.GetValueOrDefault(selection);
                selectedIsEligible = selected is not null && await IsExecutiveSourceEligibleAsync(
                    productId,
                    companyId,
                    ProductPricingReviewRules.ToPublic(currentSourceType),
                    currentSourceId.Value,
                    selected.IsEligible,
                    cancellationToken);
            }
        }
        else
        {
            var sourcesByProduct = await _realtimeSources.LoadAsync(
                [productId], companyId, normalizedCurrency, cancellationToken);
            var sources = sourcesByProduct.GetValueOrDefault(productId) ?? [];
            selected = sources.FirstOrDefault(x => x.IsCustomerSelected) ?? sources.FirstOrDefault();
            selectedIsEligible = selected?.IsEligible == true;
        }

        var customer = await ResolveCustomerAsync(productId, companyId, quotationId, cancellationToken);
        if (!customer.Success)
            return OperationResult<ProductPricingReviewDto>.Fail(customer.Message!);

        var selectedVersionNumber = selected is null
            ? null
            : await GetSourceVersionNumberAsync(selected.SourceType, selected.SourceId, companyId, cancellationToken);
        var selectedIsApplied = selected is not null && approvedStandardPricing is not null &&
            (approvedStandardPricing.SourceManufacturingFormulaId ?? approvedStandardPricing.SourceFormulaId) ==
            selected.SourceId;
        var selectedSourceNote = selected is null
            ? null
            : await ResolveSourceNoteAsync(productId, companyId, selected, cancellationToken);
        var publicSource = selected is null
            ? null
            : MapSource(
                selected,
                selectedVersionNumber,
                selectedIsApplied,
                selectedIsEligible,
                selectedSourceNote);
        var currentFormulaUse = await ResolveCurrentFormulaUseAsync(
            productId,
            companyId,
            approvedStandardPricing,
            cancellationToken);
        var sourceMaterials = selected?.Materials ?? [];
        var categories = await LoadFormulaItemCategoriesAsync(
            sourceMaterials,
            companyId,
            cancellationToken);
        var materialGroupNames = await LoadMaterialGroupNamesAsync(
            sourceMaterials,
            companyId,
            cancellationToken);
        var materials = sourceMaterials
            .Select(x => MapMaterial(
                x,
                x.CategoryId.HasValue ? categories.GetValueOrDefault(x.CategoryId.Value) : null,
                x.ItemId.HasValue ? materialGroupNames.GetValueOrDefault(x.ItemId.Value) : null))
            .OrderBy(x => x.CategoryGroup)
            .ToArray();
        var missingCount = materials.Count(x => x.Status == PricingReviewMaterialStatus.MissingPrice);
        var staleCount = materials.Count(x => x.Status == PricingReviewMaterialStatus.StalePrice);
        var materialCost = selected?.CurrentMaterialCost ?? currentVersion?.MaterialCostSnapshot;
        var manufacturingCost = currentVersion?.ManufacturingCost ?? selected?.ManufacturingCost;
        var standardPrice = currentVersion?.StandardSellingPrice ?? selected?.StandardSellingPrice;
        var costBase = materialCost.HasValue && manufacturingCost.HasValue
            ? materialCost + manufacturingCost
            : null;
        var margin = PricingMarginCalculator.CalculateProfitMarginPercent(standardPrice, costBase);
        var suggestedPriceTiers = await ResolveEditorPolicyTiersAsync(
            selected,
            productId,
            product.CategoryId,
            companyId,
            normalizedCurrency,
            materialCost,
            manufacturingCost,
            standardPrice,
            cancellationToken);
        var editorPriceTiers = ResolveEditorPriceTiers(suggestedPriceTiers);

        var counts = await LoadTabCountsAsync(productId, companyId, normalizedCurrency, cancellationToken);
        var latestFormulaConfirmedAt = await _plm.Formulas.AsNoTracking()
            .Where(x =>
                x.ProductId == productId &&
                x.CompanyId == companyId &&
                x.IsActive &&
                x.CheckDate.HasValue &&
                x.Status != FormulaStatus.Cancelled.ToString() &&
                x.Status != FormulaStatus.Rejected.ToString())
            .MaxAsync(x => x.CheckDate, cancellationToken);
        var approvedAt = approvedStandardPricing?.ApprovedAt ??
            approvedStandardPricing?.UpdatedDate ??
            approvedStandardPricing?.CreatedDate;
        var pricingReviewDueDate = approvedAt.HasValue &&
            _featureOptions.ApprovedPricingReviewAfterDays > 0
            ? approvedAt.Value.AddDays(_featureOptions.ApprovedPricingReviewAfterDays)
            : (DateTime?)null;
        var hasFormulaConfirmationPending = approvedAt.HasValue &&
            latestFormulaConfirmedAt.HasValue &&
            latestFormulaConfirmedAt.Value > approvedAt.Value;
        var isPricingReviewExpired = pricingReviewDueDate.HasValue &&
            _clock.Now >= pricingReviewDueDate.Value;
        var pricingRequests = await _requestQueryService.LoadAsync(
            companyId,
            [productId],
            cancellationToken);
        var materialCostChangedProductIds = approvedStandardPricing?.MaterialCostSnapshot is > 0m
            ? await _realtimeSources.LoadMaterialCostChangedProductIdsAsync(
                [new ProductPricingMaterialCostBaseline(
                    productId,
                    approvedStandardPricing.MaterialCostSnapshot.Value,
                    approvedStandardPricing.SourceManufacturingFormulaId.HasValue
                        ? new ProductPricingSourceSelection(
                            productId,
                            ProductPricingSourceType.ManufacturingFormula,
                            approvedStandardPricing.SourceManufacturingFormulaId.Value)
                        : approvedStandardPricing.SourceFormulaId.HasValue
                            ? new ProductPricingSourceSelection(
                                productId,
                                ProductPricingSourceType.Formula,
                                approvedStandardPricing.SourceFormulaId.Value)
                            : null)],
                companyId,
                normalizedCurrency,
                _featureOptions.MaterialCostChangeThresholdPercent,
                useLatestProductionFormula: false,
                cancellationToken)
            : new HashSet<Guid>();
        var pricingAttentionSources = ProductPricingAttentionRules.Resolve(
            approvedAt,
            pricingRequests,
            latestFormulaConfirmedAt,
            _clock.Now,
            _featureOptions,
            materialCostChangedProductIds.Contains(productId));
        var standardPriceState = approvedStandardPricing is null
            ? ProductStandardPriceState.PendingInitialApproval
            : pricingAttentionSources.Count > 0
                ? ProductStandardPriceState.PendingReapproval
                : ProductStandardPriceState.Active;
        var realtimePriceComparison = approvedStandardPricing is null
            ? null
            : await BuildRealtimeComparisonAsync(
                approvedStandardPricing,
                selected,
                companyId,
                normalizedCurrency,
                cancellationToken);
        return OperationResult<ProductPricingReviewDto>.Ok(new ProductPricingReviewDto
        {
            Header = new PricingReviewHeaderDto
            {
                ProductId = product.ProductId,
                ProductCode = product.Code,
                ProductName = product.Name,
                CustomerId = customer.Data?.CustomerId,
                CustomerCode = customer.Data?.CustomerCode,
                CustomerName = customer.Data?.CustomerName,
                Currency = normalizedCurrency,
                LastUpdatedAt = currentVersion?.UpdatedDate ?? currentVersion?.CreatedDate ?? selected?.UpdatedDate
            },
            FormulaPricingPolicyId = selected?.FormulaPricingPolicyId,
            SelectedSource = publicSource,
            CurrentFormulaUse = currentFormulaUse,
            StandardPriceState = new PricingReviewStandardPriceStateDto
            {
                State = standardPriceState,
                RequiresPricingAction = standardPriceState is ProductStandardPriceState.PendingInitialApproval or ProductStandardPriceState.PendingReapproval,
                HasFormulaConfirmationPending = hasFormulaConfirmationPending,
                IsReviewExpired = isPricingReviewExpired,
                PricingReviewDueDate = pricingReviewDueDate,
                LatestFormulaConfirmedAt = latestFormulaConfirmedAt,
                PricingAttentionSources = pricingAttentionSources
            },
            RealtimePriceComparison = realtimePriceComparison,
            Overview = new PricingReviewOverviewDto
            {
                PricingVersionId = currentVersion?.ProductPricingVersionId,
                PricingVersionNumber = currentVersion?.Version,
                StandardSellingPrice = standardPrice,
                MaterialCost = materialCost,
                ManufacturingCost = manufacturingCost,
                ProfitAmount = standardPrice.HasValue && costBase.HasValue ? standardPrice - costBase : null,
                ProfitMarginPercent = margin,
                PublisherNote = currentVersion?.PublisherNote,
                TotalMaterialCount = materials.Length,
                ReviewRequiredCount = missingCount + staleCount,
                MissingPriceCount = missingCount,
                StalePriceCount = staleCount
            },
            Materials = materials,
            Editor = new PricingReviewEditorDto
            {
                ManufacturingCost = manufacturingCost,
                StandardSellingPrice = standardPrice,
                ProfitMarginPercent = margin,
                PublisherNote = currentVersion?.PublisherNote,
                InternalNote = currentVersion?.Note,
                ExpectedUpdatedAt = currentVersion?.UpdatedDate,
                PriceTiers = editorPriceTiers
            },
            TabCounts = counts
        });
    }

    public async Task<OperationResult<PagedResult<PricingReviewSourceOptionDto>>> GetSourceOptionsAsync(
        Guid productId,
        PricingReviewSourceType? sourceType,
        string? keyword,
        string? currency,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var context = ProductPricingReviewRules.GetContext(_currentUser);
        if (!context.Success)
            return OperationResult<PagedResult<PricingReviewSourceOptionDto>>.Fail(context.Message!);
        if (!sourceType.HasValue)
            return OperationResult<PagedResult<PricingReviewSourceOptionDto>>.Fail(
                "SourceType must be VU or VA.");
        var normalizedCurrency = ProductPricingReviewRules.NormalizeCurrency(currency);
        if (normalizedCurrency is null)
            return OperationResult<PagedResult<PricingReviewSourceOptionDto>>.Fail("Only VND is supported.");
        var companyId = context.Data.CompanyId;
        if (!await ProductExistsAsync(productId, companyId, cancellationToken))
            return OperationResult<PagedResult<PricingReviewSourceOptionDto>>.Fail(
                "Product was not found, inactive, or outside the current company.");

        var approvedStandardPricing = await _crm.ProductPricingVersions.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.ProductId == productId &&
                        x.Currency == normalizedCurrency && x.IsActive &&
                        x.Status == ProductPricingStatus.Approved && x.StandardSellingPrice > 0m)
            .OrderByDescending(x => x.Version)
            .ThenByDescending(x => x.ApprovedAt ?? x.UpdatedDate ?? x.CreatedDate)
            .Select(x => new
            {
                x.ProductPricingVersionId,
                x.Version,
                x.SourceFormulaId,
                x.SourceManufacturingFormulaId
            })
            .FirstOrDefaultAsync(cancellationToken);

        var normalizedKeyword = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim();
        var keywordPattern = normalizedKeyword is null
            ? null
            : PostgresSearchPattern.ContainsLiteral(normalizedKeyword);
        var vuQuery = _plm.Formulas.AsNoTracking()
            .Where(x => x.ProductId == productId &&
                        x.CompanyId == companyId &&
                        x.IsActive &&
                        ProductPricingReviewRules.EligibleVuFormulaStatuses.Contains(x.Status));
        if (keywordPattern is not null)
            vuQuery = vuQuery.Where(x =>
                EF.Functions.ILike(x.ExternalId, keywordPattern, PostgresSearchPattern.EscapeCharacter) ||
                EF.Functions.ILike(x.Name, keywordPattern, PostgresSearchPattern.EscapeCharacter));
        var vu = sourceType == PricingReviewSourceType.VA
            ? []
            : await vuQuery.Select(x => new PricingReviewSourceOptionDto
            {
                SourceType = PricingReviewSourceType.VU,
                SourceId = x.FormulaId,
                SourceCode = x.ExternalId,
                SourceName = x.Name,
                DisplayName = x.ExternalId + " · " + x.Name,
                VersionNumber = x.FormulaVersions.Max(v => (int?)v.VersionNo),
                Status = x.Status,
                IsEligible = true,
                IsCurrentlyApplied = approvedStandardPricing != null &&
                    approvedStandardPricing.SourceFormulaId == x.FormulaId,
                UpdatedAt = x.UpdatedDate ?? x.CreatedDate
            }).ToListAsync(cancellationToken);

        var vaQuery = _plm.ManufacturingFormulas.AsNoTracking()
            .Where(x =>
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
                     version.MfgProductionOrder.Product.CompanyId == companyId)));
        if (keywordPattern is not null)
            vaQuery = vaQuery.Where(x =>
                EF.Functions.ILike(x.ExternalId, keywordPattern, PostgresSearchPattern.EscapeCharacter) ||
                EF.Functions.ILike(x.Name, keywordPattern, PostgresSearchPattern.EscapeCharacter));
        var va = sourceType == PricingReviewSourceType.VU
            ? []
            : await vaQuery.Select(x => new PricingReviewSourceOptionDto
            {
                SourceType = PricingReviewSourceType.VA,
                SourceId = x.ManufacturingFormulaId,
                SourceCode = x.ExternalId,
                SourceName = x.Name,
                DisplayName = x.ExternalId + " · " + x.Name,
                VersionNumber = x.ManufacturingFormulaVersions.Max(v => (int?)v.VersionNo),
                Status = x.Status,
                IsEligible = true,
                IsCurrentlyApplied = approvedStandardPricing != null &&
                    approvedStandardPricing.SourceManufacturingFormulaId == x.ManufacturingFormulaId,
                UpdatedAt = x.ProductionSelectVersions
                    .Where(version =>
                        version.CompanyId == companyId &&
                        version.MfgProductionOrder.CompanyId == companyId &&
                        version.MfgProductionOrder.IsActive &&
                        version.MfgProductionOrder.ProductId == productId &&
                        version.MfgProductionOrder.Product.IsActive &&
                        version.MfgProductionOrder.Product.CompanyId == companyId)
                    .Max(version => version.MfgProductionOrder.ManufacturingDate ?? version.ValidFrom) ??
                    x.UpdatedDate
            }).ToListAsync(cancellationToken);

        var all = vu.Concat(va)
            .GroupBy(x => new { x.SourceType, x.SourceId })
            .Select(x => x.OrderByDescending(y => y.IsCurrentlyApplied).ThenByDescending(y => y.UpdatedAt).First())
            .OrderByDescending(x => x.UpdatedAt)
            .ThenByDescending(x => x.IsCurrentlyApplied)
            .ThenBy(x => x.SourceType)
            .ThenBy(x => x.SourceCode)
            .ToArray();
        var normalizedPage = ProductPricingReviewRules.NormalizePageNumber(pageNumber);
        const int maximumRecentSourceCount = 5;
        var normalizedSize = Math.Min(
            ProductPricingReviewRules.NormalizePageSize(pageSize),
            maximumRecentSourceCount);
        var pageItems = all
            .Skip((normalizedPage - 1) * normalizedSize)
            .Take(normalizedSize)
            .ToArray();
        var selections = pageItems
            .Select(x => new ProductPricingSourceSelection(
                productId,
                ProductPricingReviewRules.ToLegacy(x.SourceType),
                x.SourceId))
            .ToArray();
        ProductPricingSourceSelection? standardSelection = approvedStandardPricing?.SourceManufacturingFormulaId is { } vaId
            ? new ProductPricingSourceSelection(
                productId,
                ProductPricingSourceType.ManufacturingFormula,
                vaId)
            : approvedStandardPricing?.SourceFormulaId is { } vuId
                ? new ProductPricingSourceSelection(
                    productId,
                    ProductPricingSourceType.Formula,
                    vuId)
                : null;
        var sourcesToLoad = standardSelection.HasValue
            ? selections.Append(standardSelection.Value).Distinct().ToArray()
            : selections;
        var realtimeBySource = await _realtimeSources.LoadSelectedForExecutiveAsync(
            sourcesToLoad,
            companyId,
            normalizedCurrency,
            cancellationToken);
        var currentStandardMaterialCost = standardSelection.HasValue &&
            realtimeBySource.GetValueOrDefault(standardSelection.Value) is { IsCurrentMaterialCostComplete: true } standardSource
            ? standardSource.CurrentMaterialCost
            : null;
        var enrichedItems = pageItems
            .Select(item =>
            {
                var selection = new ProductPricingSourceSelection(
                    productId,
                    ProductPricingReviewRules.ToLegacy(item.SourceType),
                    item.SourceId);
                var realtime = realtimeBySource.GetValueOrDefault(selection);
                var materialPricing = realtime is null
                    ? new PricingReviewSourceMaterialPricingDto
                    {
                        BaselineMaterialCost = currentStandardMaterialCost,
                        ComparisonStatus = PricingReviewCostComparisonStatus.Unavailable
                    }
                    : ProductPricingReviewMaterialComparisonRules.BuildSourcePricing(
                        realtime,
                        currentStandardMaterialCost,
                        _clock.Now);
                return CopySourceWithMaterialPricing(item, materialPricing);
            })
            .ToArray();
        return OperationResult<PagedResult<PricingReviewSourceOptionDto>>.Ok(
            new PagedResult<PricingReviewSourceOptionDto>(
                enrichedItems,
                all.Length,
                normalizedPage,
                normalizedSize));
    }

    private static PricingReviewSourceOptionDto CopySourceWithMaterialPricing(
        PricingReviewSourceOptionDto source,
        PricingReviewSourceMaterialPricingDto materialPricing)
        => new()
        {
            SourceType = source.SourceType,
            SourceId = source.SourceId,
            SourceCode = source.SourceCode,
            SourceName = source.SourceName,
            DisplayName = source.DisplayName,
            VersionNumber = source.VersionNumber,
            Status = source.Status,
            IsEligible = source.IsEligible,
            IsCurrentlyApplied = source.IsCurrentlyApplied,
            UpdatedAt = source.UpdatedAt,
            MaterialPricing = materialPricing
        };

    public async Task<OperationResult<PricingReviewMaterialPricePreviewDto>> GetMaterialPricePreviewAsync(
        Guid productId,
        PricingReviewSourceType sourceType,
        Guid sourceId,
        string? currency,
        int limit,
        string? sortBy,
        string? sortDirection,
        CancellationToken cancellationToken)
    {
        var context = ProductPricingReviewRules.GetContext(_currentUser);
        if (!context.Success)
            return OperationResult<PricingReviewMaterialPricePreviewDto>.Fail(context.Message!);
        if (productId == Guid.Empty || sourceId == Guid.Empty || !Enum.IsDefined(sourceType))
            return OperationResult<PricingReviewMaterialPricePreviewDto>.Fail(
                "ProductId, sourceType and sourceId are required.");

        var normalizedCurrency = ProductPricingReviewRules.NormalizeCurrency(currency);
        if (normalizedCurrency is null)
            return OperationResult<PricingReviewMaterialPricePreviewDto>.Fail("Only VND is supported.");
        var normalizedSortBy = string.IsNullOrWhiteSpace(sortBy) ? "absoluteDifference" : sortBy.Trim();
        if (!string.Equals(normalizedSortBy, "absoluteDifference", StringComparison.OrdinalIgnoreCase))
            return OperationResult<PricingReviewMaterialPricePreviewDto>.Fail(
                "SortBy must be absoluteDifference.");
        var normalizedSortDirection = string.IsNullOrWhiteSpace(sortDirection) ? "desc" : sortDirection.Trim();
        if (!string.Equals(normalizedSortDirection, "asc", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(normalizedSortDirection, "desc", StringComparison.OrdinalIgnoreCase))
            return OperationResult<PricingReviewMaterialPricePreviewDto>.Fail(
                "SortDirection must be asc or desc.");
        var descending = string.Equals(normalizedSortDirection, "desc", StringComparison.OrdinalIgnoreCase);

        var companyId = context.Data.CompanyId;
        if (!await ProductExistsAsync(productId, companyId, cancellationToken))
            return OperationResult<PricingReviewMaterialPricePreviewDto>.Fail(
                "Product was not found, inactive, or outside the current company.");

        var baseline = await _crm.ProductPricingVersions.AsNoTracking()
            .Where(x =>
                x.CompanyId == companyId &&
                x.ProductId == productId &&
                x.Currency == normalizedCurrency &&
                x.IsActive &&
                x.Status == ProductPricingStatus.Approved &&
                x.StandardSellingPrice > 0m)
            .OrderByDescending(x => x.Version)
            .ThenByDescending(x => x.ApprovedAt ?? x.UpdatedDate ?? x.CreatedDate)
            .Select(x => new
            {
                x.ProductPricingVersionId,
                x.Version,
                x.MaterialCostSnapshot,
                x.SourceFormulaId,
                x.SourceManufacturingFormulaId
            })
            .FirstOrDefaultAsync(cancellationToken);

        var viewedSelection = new ProductPricingSourceSelection(
            productId,
            ProductPricingReviewRules.ToLegacy(sourceType),
            sourceId);
        ProductPricingSourceSelection? standardSelection = baseline?.SourceManufacturingFormulaId is { } vaId
            ? new ProductPricingSourceSelection(
                productId,
                ProductPricingSourceType.ManufacturingFormula,
                vaId)
            : baseline?.SourceFormulaId is { } vuId
                ? new ProductPricingSourceSelection(
                    productId,
                    ProductPricingSourceType.Formula,
                    vuId)
                : null;
        var selections = standardSelection.HasValue
            ? new[] { viewedSelection, standardSelection.Value }.Distinct().ToArray()
            : [viewedSelection];
        var loadedSources = await _realtimeSources.LoadSelectedForExecutiveAsync(
            selections, companyId, normalizedCurrency, cancellationToken);
        var viewedSource = loadedSources.GetValueOrDefault(viewedSelection);
        var isEligible = viewedSource is not null && await IsExecutiveSourceEligibleAsync(
            productId,
            companyId,
            sourceType,
            sourceId,
            viewedSource.IsEligible,
            cancellationToken);
        if (viewedSource is null || !isEligible)
            return OperationResult<PricingReviewMaterialPricePreviewDto>.Fail(
                "Source was not found, is ineligible, or does not belong to this product and company.");

        var standardSource = standardSelection.HasValue
            ? loadedSources.GetValueOrDefault(standardSelection.Value)
            : null;
        var viewedVersionNumber = await GetSourceVersionNumberAsync(
            viewedSource.SourceType, viewedSource.SourceId, companyId, cancellationToken);
        var standardVersionNumber = standardSource is null
            ? null
            : standardSelection == viewedSelection
                ? viewedVersionNumber
                : await GetSourceVersionNumberAsync(
                    standardSource.SourceType,
                    standardSource.SourceId,
                    companyId,
                    cancellationToken);
        PricingReviewFormulaComparisonUnavailableReason? unavailableReason = standardSelection.HasValue
            ? standardSource is null
                ? PricingReviewFormulaComparisonUnavailableReason.StandardSourceUnavailable
                : null
            : PricingReviewFormulaComparisonUnavailableReason.NoStandardFormula;
        var comparisonCategories = await LoadFormulaItemCategoriesAsync(
            (standardSource?.Materials ?? [])
                .Concat(viewedSource.Materials)
                .ToArray(),
            companyId,
            cancellationToken);
        var allItems = ProductPricingReviewMaterialComparisonRules.BuildFormulaComparisonItems(
            standardSource,
            viewedSource,
            comparisonCategories);
        var returnedItems = ProductPricingReviewMaterialComparisonRules.SortFormulaComparisonItems(
            allItems,
            limit,
            descending);
        var calculatedAt = _clock.Now;

        return OperationResult<PricingReviewMaterialPricePreviewDto>.Ok(
            new PricingReviewMaterialPricePreviewDto
            {
                ViewedSource = new PricingReviewMaterialPricePreviewSourceDto
                {
                    SourceType = sourceType,
                    SourceId = viewedSource.SourceId,
                    SourceCode = viewedSource.ExternalId,
                    SourceName = viewedSource.Name,
                    DisplayName = viewedSource.ExternalId + " · " + viewedSource.Name,
                    VersionNumber = viewedVersionNumber,
                    Status = viewedSource.Status
                },
                StandardSource = standardSource is null
                    ? null
                    : new PricingReviewMaterialPricePreviewSourceDto
                    {
                        SourceType = ProductPricingReviewRules.ToPublic(standardSource.SourceType),
                        SourceId = standardSource.SourceId,
                        SourceCode = standardSource.ExternalId,
                        SourceName = standardSource.Name,
                        DisplayName = standardSource.ExternalId + " · " + standardSource.Name,
                        VersionNumber = standardVersionNumber,
                        Status = standardSource.Status,
                        IsSameAsViewedSource = standardSelection == viewedSelection
                    },
                ApprovedPricingVersion = baseline is null
                    ? null
                    : new PricingReviewApprovedVersionReferenceDto
                    {
                        PricingVersionId = baseline.ProductPricingVersionId,
                        PricingVersionNumber = baseline.Version,
                        ApprovedMaterialCostSnapshot = baseline.MaterialCostSnapshot,
                        Currency = normalizedCurrency
                    },
                Summary = ProductPricingReviewMaterialComparisonRules.BuildFormulaComparisonSummary(
                    standardSource,
                    viewedSource,
                    allItems,
                    calculatedAt,
                    unavailableReason),
                Materials = returnedItems,
                TotalCount = allItems.Count,
                ReturnedCount = returnedItems.Count
            });
    }

    private async Task<PricingReviewCurrentFormulaUseDto?> ResolveCurrentFormulaUseAsync(
        Guid productId,
        Guid companyId,
        ProductPricingVersion? approvedStandardPricing,
        CancellationToken cancellationToken)
    {
        var latestSampleSentVu = await _plm.Formulas.AsNoTracking()
            .Where(x =>
                x.ProductId == productId &&
                x.CompanyId == companyId &&
                x.IsActive &&
                x.SentDate.HasValue &&
                x.Status != FormulaStatus.Cancelled.ToString() &&
                x.Status != FormulaStatus.Rejected.ToString())
            .OrderByDescending(x => x.SentDate)
            .ThenByDescending(x => x.FormulaId)
            .Select(x => new PricingReviewFormulaUseCandidate(
                new PricingReviewCurrentFormulaUseDto
            {
                SourceType = PricingReviewSourceType.VU,
                SourceId = x.FormulaId,
                SourceCode = x.ExternalId,
                SourceName = x.Name,
                DisplayName = x.ExternalId + " · " + x.Name,
                SourceNote = NormalizeSourceNote(x.Note),
                Status = x.Status,
                IsEligible = ProductPricingReviewRules.EligibleVuFormulaStatuses.Contains(x.Status),
                IsCurrentlyApplied = approvedStandardPricing != null &&
                    approvedStandardPricing.SourceFormulaId == x.FormulaId,
                CreatedAt = x.CreatedDate
            },
                x.SentDate!.Value))
            .FirstOrDefaultAsync(cancellationToken);

        var latestProducedVa = await _plm.ProductionSelectVersions.AsNoTracking()
            .Where(x =>
                x.CompanyId == companyId &&
                x.ManufacturingFormulaId.HasValue &&
                x.ManufacturingFormula != null &&
                x.ManufacturingFormula.CompanyId == companyId &&
                x.ManufacturingFormula.IsActive &&
                x.ManufacturingFormula.Status == ManufacturingProductOrderFormula.Checking.ToString() &&
                x.MfgProductionOrder.CompanyId == companyId &&
                x.MfgProductionOrder.ProductId == productId &&
                x.MfgProductionOrder.IsActive &&
                x.MfgProductionOrder.Product.IsActive)
            .OrderByDescending(x => x.MfgProductionOrder.CreatedDate)
            .ThenByDescending(x => x.ProductionSelectVersionId)
            .Select(x => new PricingReviewFormulaUseCandidate(
                new PricingReviewCurrentFormulaUseDto
            {
                SourceType = PricingReviewSourceType.VA,
                SourceId = x.ManufacturingFormulaId!.Value,
                SourceCode = x.ManufacturingFormula!.ExternalId,
                SourceName = x.ManufacturingFormula.Name,
                DisplayName = x.ManufacturingFormula.ExternalId + " · " + x.ManufacturingFormula.Name,
                SourceNote = NormalizeSourceNote(x.ManufacturingFormula.Note),
                Status = x.ManufacturingFormula.Status,
                IsEligible = true,
                IsCurrentlyApplied = approvedStandardPricing != null &&
                    approvedStandardPricing.SourceManufacturingFormulaId == x.ManufacturingFormulaId,
                CreatedAt = x.ManufacturingFormula.CreatedDate
            },
                x.MfgProductionOrder.CreatedDate))
            .FirstOrDefaultAsync(cancellationToken);

        var latestCheckingVa = await _plm.ProductStandardFormulas.AsNoTracking()
            .Where(x =>
                x.CompanyId == companyId &&
                x.ProductId == productId &&
                x.ManufacturingFormulaId.HasValue &&
                x.ManufacturingFormula != null &&
                x.ManufacturingFormula.CompanyId == companyId &&
                x.ManufacturingFormula.IsActive &&
                x.ManufacturingFormula.Status == ManufacturingProductOrderFormula.Checking.ToString())
            .OrderByDescending(x => x.ManufacturingFormula!.CreatedDate)
            .ThenByDescending(x => x.ManufacturingFormulaId)
            .Select(x => new PricingReviewFormulaUseCandidate(
                new PricingReviewCurrentFormulaUseDto
                {
                    SourceType = PricingReviewSourceType.VA,
                    SourceId = x.ManufacturingFormulaId!.Value,
                    SourceCode = x.ManufacturingFormula!.ExternalId,
                    SourceName = x.ManufacturingFormula.Name,
                    DisplayName = x.ManufacturingFormula.ExternalId + " · " + x.ManufacturingFormula.Name,
                    SourceNote = NormalizeSourceNote(x.ManufacturingFormula.Note),
                    Status = x.ManufacturingFormula.Status,
                    IsEligible = false,
                    IsCurrentlyApplied = approvedStandardPricing != null &&
                        approvedStandardPricing.SourceManufacturingFormulaId == x.ManufacturingFormulaId,
                    CreatedAt = x.ManufacturingFormula.CreatedDate
                },
                x.ManufacturingFormula.CreatedDate))
            .FirstOrDefaultAsync(cancellationToken);

        return ProductPricingReviewRules.SelectLatestSuggestedFormulaUse(
            latestSampleSentVu,
            latestProducedVa,
            latestCheckingVa);
    }

    public async Task<OperationResult<PagedResult<PricingReviewVersionDto>>> GetVersionsAsync(
        Guid productId, string? currency, int pageNumber, int pageSize,
        string? sortBy, string? sortDirection, CancellationToken cancellationToken)
    {
        var context = ProductPricingReviewRules.GetContext(_currentUser);
        if (!context.Success)
            return OperationResult<PagedResult<PricingReviewVersionDto>>.Fail(context.Message!);
        var normalizedCurrency = ProductPricingReviewRules.NormalizeCurrency(currency);
        if (normalizedCurrency is null)
            return OperationResult<PagedResult<PricingReviewVersionDto>>.Fail("Only VND is supported.");
        var companyId = context.Data.CompanyId;
        if (!await ProductExistsAsync(productId, companyId, cancellationToken))
            return OperationResult<PagedResult<PricingReviewVersionDto>>.Fail(
                "Product was not found, inactive, or outside the current company.");

        var query = _crm.ProductPricingVersions.AsNoTracking()
            .Include(x => x.PriceTiers)
            .Include(x => x.CreatedByNavigation)
            .Include(x => x.UpdatedByNavigation)
            .Where(x => x.CompanyId == companyId && x.ProductId == productId && x.Currency == normalizedCurrency);
        var total = await query.CountAsync(cancellationToken);
        var descending = !string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase);
        query = (sortBy?.Trim().ToLowerInvariant(), descending) switch
        {
            ("versionnumber", false) => query.OrderBy(x => x.Version),
            ("status", false) => query.OrderBy(x => x.Status),
            ("status", true) => query.OrderByDescending(x => x.Status),
            ("createdat", false) => query.OrderBy(x => x.CreatedDate),
            ("updatedat", false) => query.OrderBy(x => x.UpdatedDate),
            ("updatedat", true) => query.OrderByDescending(x => x.UpdatedDate),
            _ => query.OrderByDescending(x => x.Version)
        };
        var page = ProductPricingReviewRules.NormalizePageNumber(pageNumber);
        var size = ProductPricingReviewRules.NormalizePageSize(pageSize);
        var rows = await query.Skip((page - 1) * size).Take(size).ToListAsync(cancellationToken);
        var pricingAccess = _pricingVisibilityService.GetAccess();
        var comparableRows = rows
            .Where(IsApprovedHistoryVersion)
            .ToArray();
        var selections = comparableRows
            .Select(ToSourceSelection)
            .Where(x => x is not null)
            .Cast<ProductPricingSourceSelection>()
            .Distinct()
            .ToArray();
        var sources = await _realtimeSources.LoadSelectedForExecutiveAsync(
            selections,
            companyId,
            normalizedCurrency,
            cancellationToken);
        var comparisonsByVersion = comparableRows.ToDictionary(
            x => x.ProductPricingVersionId,
            x => BuildRealtimeComparison(x, sources, pricingAccess));

        return OperationResult<PagedResult<PricingReviewVersionDto>>.Ok(
            new PagedResult<PricingReviewVersionDto>(
                rows.Select(x => MapVersion(
                    x,
                    comparisonsByVersion.GetValueOrDefault(x.ProductPricingVersionId))).ToArray(),
                total,
                page,
                size));
    }

    public async Task<OperationResult<PagedResult<PricingReviewRelatedQuotationDto>>> GetRelatedQuotationsAsync(
        Guid productId, int pageNumber, int pageSize, string? sortBy, string? sortDirection,
        CancellationToken cancellationToken)
    {
        var context = ProductPricingReviewRules.GetContext(_currentUser);
        if (!context.Success)
            return OperationResult<PagedResult<PricingReviewRelatedQuotationDto>>.Fail(context.Message!);
        var companyId = context.Data.CompanyId;
        if (!await ProductExistsAsync(productId, companyId, cancellationToken))
            return OperationResult<PagedResult<PricingReviewRelatedQuotationDto>>.Fail(
                "Product was not found, inactive, or outside the current company.");

        var query = _crm.Quotations.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.IsActive &&
                        x.Lines.Any(line => line.IsActive && line.ProductId == productId));
        var total = await query.CountAsync(cancellationToken);
        var descending = !string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase);
        query = (sortBy?.Trim().ToLowerInvariant(), descending) switch
        {
            ("quotationcode", false) => query.OrderBy(x => x.ExternalId),
            ("quotationcode", true) => query.OrderByDescending(x => x.ExternalId),
            ("quotationdate", false) => query.OrderBy(x => x.QuotationDate),
            _ => query.OrderByDescending(x => x.QuotationDate)
        };
        var page = ProductPricingReviewRules.NormalizePageNumber(pageNumber);
        var size = ProductPricingReviewRules.NormalizePageSize(pageSize);
        var rows = await query.Skip((page - 1) * size).Take(size)
            .Select(x => new PricingReviewRelatedQuotationDto
            {
                QuotationId = x.QuotationId,
                QuotationCode = x.ExternalId,
                Status = x.Status.ToString(),
                CustomerId = x.CustomerId,
                CustomerCode = x.Customer.ExternalId,
                CustomerName = x.Customer.CustomerName,
                SaleEmployeeId = x.SaleEmployeeId,
                SaleEmployeeName = x.SaleEmployee.FullName,
                Quantity = x.Lines.Where(line => line.IsActive && line.ProductId == productId).Sum(line => line.Quantity),
                Currency = x.Currency,
                QuotationDate = x.QuotationDate
            }).ToListAsync(cancellationToken);
        return OperationResult<PagedResult<PricingReviewRelatedQuotationDto>>.Ok(
            new PagedResult<PricingReviewRelatedQuotationDto>(rows, total, page, size));
    }

    public async Task<OperationResult<PagedResult<PricingReviewVaLotDto>>> GetVaLotsAsync(
        Guid productId, int pageNumber, int pageSize, string? sortBy, string? sortDirection,
        CancellationToken cancellationToken)
    {
        var context = ProductPricingReviewRules.GetContext(_currentUser);
        if (!context.Success)
            return OperationResult<PagedResult<PricingReviewVaLotDto>>.Fail(context.Message!);
        var companyId = context.Data.CompanyId;
        if (!await ProductExistsAsync(productId, companyId, cancellationToken))
            return OperationResult<PagedResult<PricingReviewVaLotDto>>.Fail(
                "Product was not found, inactive, or outside the current company.");

        var query = _plm.ProductStandardFormulas.AsNoTracking()
            .Where(x => x.ProductId == productId && x.CompanyId == companyId &&
                        x.ManufacturingFormulaId.HasValue && x.ManufacturingFormula != null &&
                        x.ManufacturingFormula.CompanyId == companyId && x.ManufacturingFormula.IsActive);
        var total = await query.CountAsync(cancellationToken);
        var descending = !string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase);
        query = (sortBy?.Trim().ToLowerInvariant(), descending) switch
        {
            ("externalid", false) => query.OrderBy(x => x.ManufacturingFormula!.ExternalId),
            ("externalid", true) => query.OrderByDescending(x => x.ManufacturingFormula!.ExternalId),
            ("effectivefrom", false) => query.OrderBy(x => x.ValidFrom),
            _ => query.OrderByDescending(x => x.ValidFrom)
        };
        var page = ProductPricingReviewRules.NormalizePageNumber(pageNumber);
        var size = ProductPricingReviewRules.NormalizePageSize(pageSize);
        var now = _clock.Now;
        var rows = await query.Skip((page - 1) * size).Take(size)
            .Select(x => new PricingReviewVaLotDto
            {
                ManufacturingFormulaId = x.ManufacturingFormulaId!.Value,
                ExternalId = x.ManufacturingFormula!.ExternalId,
                Name = x.ManufacturingFormula.Name,
                DisplayName = x.ManufacturingFormula.ExternalId + " · " + x.ManufacturingFormula.Name,
                VersionNumber = x.ManufacturingFormula.ManufacturingFormulaVersions.Max(v => (int?)v.VersionNo),
                Status = x.ManufacturingFormula.Status,
                IsCurrentlyApplied = x.ValidFrom <= now && (!x.ValidTo.HasValue || x.ValidTo >= now),
                EffectiveFrom = x.ValidFrom,
                EffectiveTo = x.ValidTo
            }).ToListAsync(cancellationToken);
        return OperationResult<PagedResult<PricingReviewVaLotDto>>.Ok(
            new PagedResult<PricingReviewVaLotDto>(rows, total, page, size));
    }

    internal static PricingReviewVersionDto MapVersion(
        ProductPricingVersion x,
        StandardPriceRealtimeComparisonDto? realtimePriceComparison = null)
    {
        var costBase = x.MaterialCostSnapshot.HasValue && x.ManufacturingCost.HasValue
            ? x.MaterialCostSnapshot + x.ManufacturingCost
            : null;
        return new PricingReviewVersionDto
        {
            PricingVersionId = x.ProductPricingVersionId,
            VersionNumber = x.Version,
            Status = x.Status.ToString(),
            Currency = x.Currency,
            SourceType = x.SourceManufacturingFormulaId.HasValue
                ? PricingReviewSourceType.VA
                : x.SourceFormulaId.HasValue ? PricingReviewSourceType.VU : null,
            SourceId = x.SourceManufacturingFormulaId ?? x.SourceFormulaId,
            SourceCode = x.FormulaExternalIdSnapshot,
            MaterialCost = x.MaterialCostSnapshot,
            ManufacturingCost = x.ManufacturingCost,
            StandardSellingPrice = x.StandardSellingPrice,
            RealtimePriceComparison = realtimePriceComparison,
            ProfitAmount = x.StandardSellingPrice.HasValue && costBase.HasValue ? x.StandardSellingPrice - costBase : null,
            ProfitMarginPercent = PricingMarginCalculator.CalculateProfitMarginPercent(
                x.StandardSellingPrice,
                costBase),
            PublisherNote = x.PublisherNote,
            InternalNote = x.Note,
            EffectiveFrom = x.ApprovedAt,
            EffectiveTo = x.Status == ProductPricingStatus.Superseded ? x.UpdatedDate : null,
            CreatedByEmployeeId = x.CreatedBy,
            CreatedByName = x.CreatedByNavigation?.FullName,
            UpdatedByEmployeeId = x.UpdatedBy,
            UpdatedByName = x.UpdatedByNavigation?.FullName,
            CreatedAt = x.CreatedDate,
            UpdatedAt = x.UpdatedDate,
            PriceTiers = x.PriceTiers.OrderBy(t => t.SortOrder).Select(MapTier).ToArray()
        };
    }

    private StandardPriceRealtimeComparisonDto? BuildRealtimeComparison(
        ProductPricingVersion version,
        IReadOnlyDictionary<ProductPricingSourceSelection, ProductPricingSourceOptionDto> sources,
        PricingAccessDecision pricingAccess)
    {
        var comparison = _comparisonQueryService.BuildVisible(
            [new StandardPriceRealtimeComparisonRequest(
                version.ProductId,
                version.Currency,
                version.StandardSellingPrice,
                version.MaterialCostSnapshot,
                version.SourceManufacturingFormulaId.HasValue
                    ? ProductPricingSourceType.ManufacturingFormula
                    : version.SourceFormulaId.HasValue ? ProductPricingSourceType.Formula : null,
                version.SourceManufacturingFormulaId ?? version.SourceFormulaId)],
            sources,
            pricingAccess);

        return comparison.GetValueOrDefault(version.ProductId);
    }

    private async Task<StandardPriceRealtimeComparisonDto?> BuildRealtimeComparisonAsync(
        ProductPricingVersion approvedVersion,
        ProductPricingSourceOptionDto? selectedSource,
        Guid companyId,
        string currency,
        CancellationToken cancellationToken)
    {
        var approvedSelection = ToSourceSelection(approvedVersion);
        IReadOnlyDictionary<ProductPricingSourceSelection, ProductPricingSourceOptionDto> sources;
        if (approvedSelection is null)
        {
            sources = new Dictionary<ProductPricingSourceSelection, ProductPricingSourceOptionDto>();
        }
        else if (selectedSource is not null &&
                 selectedSource.SourceType == approvedSelection.Value.SourceType &&
                 selectedSource.SourceId == approvedSelection.Value.SourceId)
        {
            sources = new Dictionary<ProductPricingSourceSelection, ProductPricingSourceOptionDto>
            {
                [approvedSelection.Value] = selectedSource
            };
        }
        else
        {
            sources = await _realtimeSources.LoadSelectedForExecutiveAsync(
                [approvedSelection.Value],
                companyId,
                currency,
                cancellationToken);
        }

        return BuildRealtimeComparison(
            approvedVersion,
            sources,
            _pricingVisibilityService.GetAccess());
    }

    private static bool IsApprovedHistoryVersion(ProductPricingVersion version)
        => version.Status is ProductPricingStatus.Approved or ProductPricingStatus.Superseded;

    private static ProductPricingSourceSelection? ToSourceSelection(ProductPricingVersion version)
    {
        if (version.SourceManufacturingFormulaId is { } manufacturingFormulaId)
        {
            return new ProductPricingSourceSelection(
                version.ProductId,
                ProductPricingSourceType.ManufacturingFormula,
                manufacturingFormulaId);
        }

        return version.SourceFormulaId is { } formulaId
            ? new ProductPricingSourceSelection(
                version.ProductId,
                ProductPricingSourceType.Formula,
                formulaId)
            : null;
    }

    internal static PricingReviewPriceTierDto MapTier(ProductPricingTier x) => new()
    {
        PricingTierId = x.ProductPricingTierId,
        QuantityRangeLabel = x.QuantityRangeLabel,
        MinQuantity = x.MinQuantity,
        MaxQuantity = x.MaxQuantity,
        MinInclusive = x.MinInclusive,
        MaxInclusive = x.MaxInclusive,
        UnitPrice = x.UnitPrice,
        SortOrder = x.SortOrder,
        IsActive = x.IsActive,
        RequiresManualPrice = false,
        IsStored = true
    };

    internal static IReadOnlyList<PricingReviewPriceTierDto> ResolveEditorPriceTiers(
        IReadOnlyList<HRM.Application.Commons.Pricing.Dtos.FormulaSuggestedPriceTierDto> suggestedTiers)
        => suggestedTiers
            .OrderBy(x => x.SortOrder)
            .Select(x => new PricingReviewPriceTierDto
            {
                PricingTierId = null,
                QuantityRangeLabel = x.QuantityRangeLabel,
                MinQuantity = x.MinQuantity,
                MaxQuantity = x.MaxQuantity,
                MinInclusive = x.MinInclusive,
                MaxInclusive = x.MaxInclusive,
                UnitPrice = x.UnitPrice,
                SortOrder = x.SortOrder,
                IsActive = true,
                RequiresManualPrice = x.RequiresManualPrice,
                IsStored = false
            })
            .ToArray();

    private async Task<IReadOnlyList<HRM.Application.Commons.Pricing.Dtos.FormulaSuggestedPriceTierDto>>
        ResolveEditorPolicyTiersAsync(
            ProductPricingSourceOptionDto? selected,
            Guid productId,
            Guid categoryId,
            Guid companyId,
            string currency,
            decimal? materialCost,
            decimal? manufacturingCost,
            decimal? standardSellingPrice,
            CancellationToken cancellationToken)
    {
        if (selected?.PricingProfile is not { } profile)
            return [];

        if (standardSellingPrice.HasValue)
        {
            var policy = await _pricingPolicyResolver.GetPublishedAsync(
                companyId,
                categoryId,
                profile,
                currency,
                cancellationToken);
            return policy is null
                ? []
                : BuildPolicyTiersForStandardSellingPrice(policy, standardSellingPrice.Value);
        }

        var result = await _pricingEngine.ResolveAsync(new PricingEngineRequest
        {
            CompanyId = companyId,
            CategoryId = categoryId,
            ProductId = productId,
            SourceId = selected.SourceId,
            SourceType = selected.SourceType.ToString(),
            Profile = profile,
            Currency = currency,
            MaterialCost = materialCost,
            ManufacturingCostOverride = manufacturingCost,
            StandardSellingPrice = standardSellingPrice
        }, cancellationToken);

        return result.Success && result.Data is not null
            ? result.Data.SuggestedTiers
            : [];
    }

    private static IReadOnlyList<HRM.Application.Commons.Pricing.Dtos.FormulaSuggestedPriceTierDto>
        BuildPolicyTiersForStandardSellingPrice(
            ResolvedFormulaPricingPolicy policy,
            decimal standardSellingPrice)
        => policy.Definition.Tiers
            .OrderBy(x => x.SortOrder)
            .Select(x => new HRM.Application.Commons.Pricing.Dtos.FormulaSuggestedPriceTierDto
            {
                QuantityRangeLabel = x.QuantityRangeLabel,
                MinQuantity = x.MinQuantity,
                MaxQuantity = x.MaxQuantity,
                MinInclusive = x.MinInclusive,
                MaxInclusive = x.MaxInclusive,
                UnitPrice = x.PriceOffset.HasValue
                    ? PricingRoundingRules.RoundCalculatedPrice(
                        Math.Max(0m, standardSellingPrice + x.PriceOffset.Value),
                        policy.Definition.RoundingRule,
                        policy.Definition.RoundingIncrement)
                    : null,
                RequiresManualPrice = !x.PriceOffset.HasValue,
                SortOrder = x.SortOrder
            })
            .ToArray();

    private PricingReviewMaterialDto MapMaterial(
        QuotationProductPricingMaterialDto x,
        PricingReviewFormulaItemCategory? category,
        string? groupName)
    {
        var identity = ProductPricingReviewItemIdentity.Resolve(x.ItemType, x.ItemId);
        var status = !x.HasLatestPrice
            ? PricingReviewMaterialStatus.MissingPrice
            : x.LatestPriceDate.HasValue && x.LatestPriceDate < _clock.Now.AddDays(-ProductPricingReviewRules.StalePriceDays)
                ? PricingReviewMaterialStatus.StalePrice
                : PricingReviewMaterialStatus.UpToDate;
        var categoryGroup = ProductPricingReviewMaterialCategoryRules.Resolve(
            category?.CategoryCode,
            category?.CategoryName);
        return new PricingReviewMaterialDto
        {
            FormulaMaterialId = x.FormulaMaterialId,
            MaterialId = identity.MaterialId,
            ProductId = identity.ProductId,
            MaterialCode = x.ItemCode,
            MaterialName = x.ItemName,
            CategoryId = x.CategoryId,
            CategoryName = category?.CategoryName,
            CategoryGroup = categoryGroup,
            CategoryGroupName = ProductPricingReviewMaterialCategoryRules.GetDisplayName(categoryGroup),
            MaterialType = identity.ItemType.ToString(),
            GroupName = groupName,
            Quantity = x.Quantity,
            Unit = x.Unit,
            CurrentUnitPrice = x.LatestUnitPrice,
            MaterialAmount = x.LatestTotalPrice,
            PriceDate = x.LatestPriceDate,
            PriceSource = x.LatestPriceSource.ToString(),
            Price = new PricingReviewMaterialPriceDto
            {
                LatestPriceDate = x.LatestPriceDate,
                UnitPrice = x.LatestUnitPrice,
                Source = x.LatestPriceSource,
                Calculation = x.PriceCalculation
            },
            Status = status
        };
    }

    private async Task<IReadOnlyDictionary<Guid, PricingReviewFormulaItemCategory>> LoadFormulaItemCategoriesAsync(
        IReadOnlyCollection<QuotationProductPricingMaterialDto> sourceMaterials,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var categoryIds = sourceMaterials
            .Where(x => x.CategoryId.HasValue)
            .Select(x => x.CategoryId!.Value)
            .Distinct()
            .ToArray();
        if (categoryIds.Length == 0)
            return new Dictionary<Guid, PricingReviewFormulaItemCategory>();

        return await _plm.Categories.AsNoTracking()
            .Where(x =>
                categoryIds.Contains(x.CategoryId) &&
                x.CompanyId == companyId)
            .Select(x => new PricingReviewFormulaItemCategory
            {
                CategoryId = x.CategoryId,
                CategoryCode = x.ExternalId,
                CategoryName = x.Name
            })
            .ToDictionaryAsync(x => x.CategoryId, cancellationToken);
    }

    private async Task<IReadOnlyDictionary<Guid, string?>> LoadMaterialGroupNamesAsync(
        IReadOnlyCollection<QuotationProductPricingMaterialDto> sourceMaterials,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var materialIds = sourceMaterials
            .Where(x =>
                x.ItemId.HasValue &&
                ProductPricingReviewItemIdentity.Resolve(x.ItemType, x.ItemId).MaterialId.HasValue)
            .Select(x => x.ItemId!.Value)
            .Distinct()
            .ToArray();
        if (materialIds.Length == 0)
            return new Dictionary<Guid, string?>();

        return await _plm.Materials.AsNoTracking()
            .Where(x =>
                materialIds.Contains(x.MaterialId) &&
                x.CompanyId == companyId)
            .Select(x => new
            {
                MaterialId = x.MaterialId,
                GroupName = x.MaterialGroupNames
                    .Where(group => group.IsActive)
                    .OrderBy(group => group.MaterialGroupNameText)
                    .Select(group => group.MaterialGroupNameText)
                    .FirstOrDefault()
            })
            .ToDictionaryAsync(
                x => x.MaterialId,
                x => x.GroupName,
                cancellationToken);
    }

    private static PricingReviewSourceOptionDto MapSource(
        ProductPricingSourceOptionDto x,
        int? versionNumber,
        bool isCurrentlyApplied,
        bool isEligible,
        string? sourceNote) => new()
    {
        SourceType = ProductPricingReviewRules.ToPublic(x.SourceType),
        SourceId = x.SourceId,
        SourceCode = x.ExternalId,
        SourceName = x.Name,
        DisplayName = x.ExternalId + " · " + x.Name,
        SourceNote = sourceNote,
        VersionNumber = versionNumber,
        Status = x.Status,
        IsEligible = isEligible,
        IsCurrentlyApplied = isCurrentlyApplied,
        UpdatedAt = x.UpdatedDate
    };

    private async Task<string?> ResolveSourceNoteAsync(
        Guid productId,
        Guid companyId,
        ProductPricingSourceOptionDto source,
        CancellationToken cancellationToken)
    {
        string? note;
        if (source.SourceType == ProductPricingSourceType.Formula)
        {
            note = await _plm.Formulas.AsNoTracking()
                .Where(x =>
                    x.FormulaId == source.SourceId &&
                    x.ProductId == productId &&
                    x.CompanyId == companyId &&
                    x.IsActive)
                .Select(x => x.Note)
                .FirstOrDefaultAsync(cancellationToken);
        }
        else
        {
            note = await _plm.ManufacturingFormulas.AsNoTracking()
                .Where(x =>
                    x.ManufacturingFormulaId == source.SourceId &&
                    x.CompanyId == companyId &&
                    x.IsActive &&
                    (x.ProductStandardFormulas.Any(link =>
                         link.CompanyId == companyId &&
                         link.ProductId == productId &&
                         link.Product.IsActive &&
                         link.Product.CompanyId == companyId) ||
                     x.ProductionSelectVersions.Any(version =>
                         version.CompanyId == companyId &&
                         version.MfgProductionOrder.CompanyId == companyId &&
                         version.MfgProductionOrder.IsActive &&
                         version.MfgProductionOrder.ProductId == productId &&
                         version.MfgProductionOrder.Product.IsActive &&
                         version.MfgProductionOrder.Product.CompanyId == companyId)))
                .Select(x => x.Note)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return NormalizeSourceNote(note);
    }

    private static string? NormalizeSourceNote(string? note)
        => string.IsNullOrWhiteSpace(note) ? null : note.Trim();

    private async Task<bool> IsExecutiveSourceEligibleAsync(
        Guid productId,
        Guid companyId,
        PricingReviewSourceType sourceType,
        Guid sourceId,
        bool canonicalEligibility,
        CancellationToken cancellationToken)
    {
        if (sourceType == PricingReviewSourceType.VU)
        {
            if (canonicalEligibility)
                return true;

            return await _plm.Formulas.AsNoTracking().AnyAsync(x =>
                x.FormulaId == sourceId &&
                x.ProductId == productId &&
                x.CompanyId == companyId &&
                x.IsActive &&
                ProductPricingReviewRules.EligibleVuFormulaStatuses.Contains(x.Status),
                cancellationToken);
        }

        return await _plm.ManufacturingFormulas.AsNoTracking().AnyAsync(x =>
            x.ManufacturingFormulaId == sourceId &&
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
            cancellationToken);
    }

    private async Task<int?> GetSourceVersionNumberAsync(
        ProductPricingSourceType type, Guid sourceId, Guid companyId, CancellationToken cancellationToken)
        => type == ProductPricingSourceType.Formula
            ? await _plm.FormulaVersions.AsNoTracking()
                .Where(x => x.FormulaId == sourceId && x.Formula.CompanyId == companyId)
                .MaxAsync(x => (int?)x.VersionNo, cancellationToken)
            : await _plm.ManufacturingFormulaVersions.AsNoTracking()
                .Where(x => x.ManufacturingFormulaId == sourceId && x.ManufacturingFormula.CompanyId == companyId)
                .MaxAsync(x => (int?)x.VersionNo, cancellationToken);

    private async Task<OperationResult<CustomerRow?>> ResolveCustomerAsync(
        Guid productId, Guid companyId, Guid? quotationId, CancellationToken cancellationToken)
    {
        if (quotationId.HasValue)
        {
            var quotation = await _crm.Quotations.AsNoTracking()
                .Where(x => x.QuotationId == quotationId && x.CompanyId == companyId && x.IsActive &&
                            x.Lines.Any(line => line.IsActive && line.ProductId == productId))
                .Select(x => new CustomerRow(x.CustomerId, x.Customer.ExternalId, x.Customer.CustomerName))
                .FirstOrDefaultAsync(cancellationToken);
            return quotation is null
                ? OperationResult<CustomerRow?>.Fail(
                    "Quotation was not found, outside the current company, or does not contain this product.")
                : OperationResult<CustomerRow?>.Ok(quotation);
        }

        var customer = await _plm.SampleRequests.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.ProductId == productId && x.IsActive &&
                        x.Customer.CompanyId == companyId && x.Customer.IsActive == true)
            .OrderByDescending(x => x.UpdatedDate ?? x.CreatedDate)
            .Select(x => new CustomerRow(x.CustomerId, x.Customer.ExternalId, x.Customer.CustomerName))
            .FirstOrDefaultAsync(cancellationToken);
        return OperationResult<CustomerRow?>.Ok(customer);
    }

    private async Task<PricingReviewTabCountsDto> LoadTabCountsAsync(
        Guid productId, Guid companyId, string currency, CancellationToken cancellationToken)
        => new()
        {
            PricingHistoryCount = await _crm.ProductPricingVersions.AsNoTracking()
                .CountAsync(x => x.CompanyId == companyId && x.ProductId == productId && x.Currency == currency,
                    cancellationToken),
            RelatedQuotationCount = await _crm.Quotations.AsNoTracking()
                .CountAsync(x => x.CompanyId == companyId && x.IsActive &&
                                 x.Lines.Any(line => line.IsActive && line.ProductId == productId),
                    cancellationToken),
            VaLotCount = await _plm.ProductStandardFormulas.AsNoTracking()
                .CountAsync(x => x.CompanyId == companyId && x.ProductId == productId &&
                                 x.ManufacturingFormulaId.HasValue && x.ManufacturingFormula != null &&
                                 x.ManufacturingFormula.IsActive,
                    cancellationToken)
        };

    private Task<bool> ProductExistsAsync(Guid productId, Guid companyId, CancellationToken cancellationToken)
        => _plm.Products.AsNoTracking().AnyAsync(
            x => x.ProductId == productId && x.CompanyId == companyId && x.IsActive,
            cancellationToken);

    private sealed record CustomerRow(Guid CustomerId, string CustomerCode, string CustomerName);
}
