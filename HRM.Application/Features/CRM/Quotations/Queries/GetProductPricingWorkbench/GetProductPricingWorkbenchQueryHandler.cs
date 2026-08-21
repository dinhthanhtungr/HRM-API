using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Domain.Enums.CustomerEnum;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Queries.GetProductPricingWorkbench;

internal sealed class GetProductPricingWorkbenchQueryHandler
    : IRequestHandler<
        GetProductPricingWorkbenchQuery,
        OperationResult<PagedResult<ProductPricingWorkbenchItemDto>>>
{
    private readonly ICRMReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly ProductPricingRealtimeSourceQueryService _sourceQueryService;
    private readonly ProductPricingRequestQueryService _requestQueryService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public GetProductPricingWorkbenchQueryHandler(
        ICRMReadDbContext dbContext,
        ICurrentUser currentUser,
        ProductPricingRealtimeSourceQueryService sourceQueryService,
        ProductPricingRequestQueryService requestQueryService,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _sourceQueryService = sourceQueryService;
        _requestQueryService = requestQueryService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<OperationResult<PagedResult<ProductPricingWorkbenchItemDto>>> Handle(
        GetProductPricingWorkbenchQuery request,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateAccess(
            _currentUser,
            request.NormalizedCurrency,
            request.View);
        if (validationError is not null)
        {
            return OperationResult<PagedResult<ProductPricingWorkbenchItemDto>>.Fail(
                validationError);
        }

        var canManagePricing = ProductPricingAccessRules.CanManage(_currentUser);
        var companyId = _currentUser.CompanyId!.Value;
        var now = _dateTimeProvider.Now;
        var products = await _dbContext.Products
            .AsNoTracking()
            .Where(x => x.IsActive && x.CompanyId == companyId)
            .Select(x => new ProductRow
            {
                ProductId = x.ProductId,
                ProductCode = x.ColourCode ?? x.Code ?? string.Empty,
                ProductName = x.Name ?? string.Empty,
                CreatedDate = x.CreatedDate,
                UpdatedDate = x.UpdatedDate,
                LatestSampleRequestCreatedDate = x.SampleRequests
                    .Where(sampleRequest =>
                        sampleRequest.IsActive &&
                        sampleRequest.CompanyId == companyId)
                    .Max(sampleRequest => (DateTime?)sampleRequest.CreatedDate),
                HasEligiblePricingSource = x.Formulas.Any(formula =>
                        formula.IsActive &&
                        formula.CompanyId == companyId &&
                        ProductPricingSourceRules.EligibleFormulaStatuses.Contains(formula.Status)) ||
                    x.ProductStandardFormulas.Any(standard =>
                        standard.CompanyId == companyId &&
                        standard.ValidFrom <= now &&
                        (!standard.ValidTo.HasValue || standard.ValidTo >= now) &&
                        standard.ManufacturingFormulaId.HasValue &&
                        standard.ManufacturingFormula != null &&
                        standard.ManufacturingFormula.IsActive &&
                        standard.ManufacturingFormula.CompanyId == companyId &&
                        (ProductPricingSourceRules.EligibleManufacturingFormulaStatuses.Contains(
                             standard.ManufacturingFormula.Status) ||
                         standard.ManufacturingFormula.ManufacturingFormulaVersions.Any(version =>
                             version.Status ==
                                 ProductPricingSourceRules.ReleasedManufacturingVersionStatus)))
            })
            .ToListAsync(cancellationToken);
        if (products.Count == 0)
        {
            return EmptyResult(request);
        }

        var productIds = products.Select(x => x.ProductId).ToArray();
        var versions = await LoadVersionRowsAsync(
            companyId,
            request.NormalizedCurrency,
            productIds,
            cancellationToken);
        var versionsByProduct = versions
            .GroupBy(x => x.ProductId)
            .ToDictionary(x => x.Key, x => (IReadOnlyList<PricingVersionRow>)x.ToArray());
        var requests = await _requestQueryService.LoadAsync(
            companyId,
            request.NormalizedCurrency,
            productIds: null,
            cancellationToken);
        var requestsByProduct = requests
            .GroupBy(x => x.ProductId)
            .ToDictionary(x => x.Key, x => (IReadOnlyList<ProductPricingRequestRow>)x.ToArray());
        var pendingRequestsByProduct = requestsByProduct
            .Where(x => !(versionsByProduct.GetValueOrDefault(x.Key) ?? [])
                .Any(version => version.Status == ProductPricingStatus.Approved))
            .ToDictionary(x => x.Key, x => x.Value);

        var filtered = products
            .Where(product => MatchesView(
                request.View,
                product.HasEligiblePricingSource,
                versionsByProduct.GetValueOrDefault(product.ProductId) ?? [],
                pendingRequestsByProduct.ContainsKey(product.ProductId)))
            .Where(product => MatchesKeyword(
                product,
                versionsByProduct.GetValueOrDefault(product.ProductId) ?? [],
                requestsByProduct.GetValueOrDefault(product.ProductId) ?? [],
                request.NormalizedKeyword))
            .ToArray();
        var ordered = ApplySorting(filtered, pendingRequestsByProduct, request);
        var totalCount = ordered.Length;
        var pageProducts = ordered
            .Skip((request.NormalizedPageNumber - 1) * request.NormalizedPageSize)
            .Take(request.NormalizedPageSize)
            .ToArray();
        if (pageProducts.Length == 0)
        {
            return OperationResult<PagedResult<ProductPricingWorkbenchItemDto>>.Ok(
                new PagedResult<ProductPricingWorkbenchItemDto>(
                    [],
                    totalCount,
                    request.NormalizedPageNumber,
                    request.NormalizedPageSize));
        }

        var pageProductIds = pageProducts.Select(x => x.ProductId).ToArray();
        var currentByProduct = pageProductIds.ToDictionary(
            productId => productId,
            productId => ResolveCurrentVersions(
                versionsByProduct.GetValueOrDefault(productId) ?? []));
        var storedSelections = currentByProduct.Values
            .Select(x => x.Preferred)
            .Where(x => x is not null && x.SourceId.HasValue && x.SourceType.HasValue)
            .Select(x => new ProductPricingSourceSelection(
                x!.ProductId,
                x.SourceType!.Value,
                x.SourceId!.Value))
            .ToArray();
        var storedSources = await _sourceQueryService.LoadSelectedAsync(
            storedSelections,
            companyId,
            request.NormalizedCurrency,
            cancellationToken);
        var productsWithoutStoredPricing = currentByProduct
            .Where(x => x.Value.Preferred is null)
            .Select(x => x.Key)
            .ToArray();
        var fallbackSources = await _sourceQueryService.LoadAsync(
            productsWithoutStoredPricing,
            companyId,
            request.NormalizedCurrency,
            cancellationToken);

        var items = pageProducts
            .Select(product =>
            {
                var current = currentByProduct[product.ProductId];
                var source = ResolveSource(
                    product.ProductId,
                    current.Preferred,
                    storedSources,
                    fallbackSources);
                var productRequests = requestsByProduct.GetValueOrDefault(product.ProductId) ?? [];
                return ProductPricingWorkbenchVisibility.ApplyToSummary(
                    ProductPricingWorkbenchMapper.MapSummary(
                        product,
                        request.NormalizedCurrency,
                        current.Draft,
                        current.Approved,
                        source,
                        productRequests),
                    canManagePricing);
            })
            .ToArray();

        return OperationResult<PagedResult<ProductPricingWorkbenchItemDto>>.Ok(
            new PagedResult<ProductPricingWorkbenchItemDto>(
                items,
                totalCount,
                request.NormalizedPageNumber,
                request.NormalizedPageSize));
    }

    private async Task<IReadOnlyList<PricingVersionRow>> LoadVersionRowsAsync(
        Guid companyId,
        string currency,
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken)
        => await _dbContext.ProductPricingVersions
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == companyId &&
                x.IsActive &&
                x.Currency == currency &&
                productIds.Contains(x.ProductId) &&
                (x.Status == ProductPricingStatus.Draft ||
                 x.Status == ProductPricingStatus.Approved))
            .Select(x => new PricingVersionRow
            {
                ProductPricingVersionId = x.ProductPricingVersionId,
                ProductId = x.ProductId,
                SourceType = x.SourceManufacturingFormulaId.HasValue
                    ? ProductPricingSourceType.ManufacturingFormula
                    : x.SourceFormulaId.HasValue
                        ? ProductPricingSourceType.Formula
                        : null,
                SourceId = x.SourceManufacturingFormulaId ?? x.SourceFormulaId,
                SourceExternalId = x.FormulaExternalIdSnapshot,
                MaterialCostSnapshot = x.MaterialCostSnapshot,
                ManufacturingCost = x.ManufacturingCost,
                StandardSellingPrice = x.StandardSellingPrice,
                ProfitMarginRate = x.ProfitMarginRate,
                Status = x.Status,
                Version = x.Version,
                CreatedDate = x.CreatedDate,
                UpdatedDate = x.UpdatedDate
            })
            .ToListAsync(cancellationToken);

    private static bool MatchesView(
        ProductPricingWorkbenchView view,
        bool hasEligiblePricingSource,
        IReadOnlyList<PricingVersionRow> versions,
        bool hasQuotationRequest)
    {
        var hasDraft = versions.Any(x => x.Status == ProductPricingStatus.Draft);
        var hasApproved = versions.Any(x => x.Status == ProductPricingStatus.Approved);
        return view switch
        {
            ProductPricingWorkbenchView.NeedsPricing => hasQuotationRequest && !hasApproved,
            ProductPricingWorkbenchView.Draft => hasDraft,
            ProductPricingWorkbenchView.Approved => hasApproved,
            ProductPricingWorkbenchView.All =>
                hasQuotationRequest || versions.Count > 0 || hasEligiblePricingSource,
            _ => false
        };
    }

    private static bool MatchesKeyword(
        ProductRow product,
        IReadOnlyList<PricingVersionRow> versions,
        IReadOnlyList<ProductPricingRequestRow> requests,
        string? keyword)
    {
        if (keyword is null)
        {
            return true;
        }

        return product.ProductCode.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
            product.ProductName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
            versions.Any(x =>
                x.SourceExternalId?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true) ||
            requests.Any(x =>
                x.QuotationExternalId.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                x.CustomerExternalId.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                x.CustomerName.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    internal static ProductRow[] ApplySorting(
        IReadOnlyCollection<ProductRow> products,
        IReadOnlyDictionary<Guid, IReadOnlyList<ProductPricingRequestRow>> requestsByProduct,
        GetProductPricingWorkbenchQuery request)
    {
        Func<ProductRow, DateTime?> latestRequestedAt = product =>
            requestsByProduct
                .GetValueOrDefault(product.ProductId)?
                .Max(x => x.RequestedAt);

        return request.NormalizedSortBy?.ToLowerInvariant() switch
        {
            GetProductPricingWorkbenchSortFields.ProductCode =>
                (request.SortDescending
                    ? products.OrderByDescending(x => x.ProductCode)
                    : products.OrderBy(x => x.ProductCode))
                .ThenByDescending(x => latestRequestedAt(x))
                .ThenByDescending(x => x.LatestSampleRequestCreatedDate)
                .ThenBy(x => x.ProductId)
                .ToArray(),

            GetProductPricingWorkbenchSortFields.ProductName =>
                (request.SortDescending
                    ? products.OrderByDescending(x => x.ProductName)
                    : products.OrderBy(x => x.ProductName))
                .ThenByDescending(x => latestRequestedAt(x))
                .ThenByDescending(x => x.LatestSampleRequestCreatedDate)
                .ThenBy(x => x.ProductId)
                .ToArray(),

            GetProductPricingWorkbenchSortFields.RequestedAt =>
                (request.SortDescending
                    ? products.OrderByDescending(x => latestRequestedAt(x))
                    : products.OrderBy(x => latestRequestedAt(x)))
                .ThenByDescending(x => x.LatestSampleRequestCreatedDate)
                .ThenBy(x => x.ProductCode)
                .ThenBy(x => x.ProductId)
                .ToArray(),

            GetProductPricingWorkbenchSortFields.CreatedDate =>
                (request.SortDescending
                    ? products.OrderByDescending(x => x.CreatedDate)
                    : products.OrderBy(x => x.CreatedDate))
                .ThenByDescending(x => x.UpdatedDate)
                .ThenByDescending(x => x.ProductId)
                .ToArray(),

            _ => products
                .OrderByDescending(x => latestRequestedAt(x).HasValue)
                .ThenByDescending(x => latestRequestedAt(x))
                .ThenByDescending(x => x.LatestSampleRequestCreatedDate)
                .ThenByDescending(x => x.UpdatedDate ?? x.CreatedDate)
                .ThenBy(x => x.ProductCode)
                .ThenBy(x => x.ProductId)
                .ToArray()
        };
    }

    private static CurrentPricingRows ResolveCurrentVersions(
        IReadOnlyList<PricingVersionRow> versions)
    {
        var draft = Latest(versions, ProductPricingStatus.Draft);
        var approved = Latest(versions, ProductPricingStatus.Approved);
        return new CurrentPricingRows(draft, approved);
    }

    private static PricingVersionRow? Latest(
        IReadOnlyList<PricingVersionRow> versions,
        ProductPricingStatus status)
        => versions
            .Where(x => x.Status == status)
            .OrderByDescending(x => x.Version)
            .ThenByDescending(x => x.UpdatedDate ?? x.CreatedDate)
            .FirstOrDefault();

    private static ProductPricingSourceOptionDto? ResolveSource(
        Guid productId,
        PricingVersionRow? preferred,
        IReadOnlyDictionary<ProductPricingSourceSelection, ProductPricingSourceOptionDto> storedSources,
        IReadOnlyDictionary<Guid, IReadOnlyList<ProductPricingSourceOptionDto>> fallbackSources)
    {
        if (preferred?.SourceType is { } sourceType && preferred.SourceId is { } sourceId)
        {
            return storedSources.GetValueOrDefault(
                new ProductPricingSourceSelection(productId, sourceType, sourceId));
        }

        return ProductPricingWorkbenchSourceSelector.ChooseFallback(
            fallbackSources.GetValueOrDefault(productId) ?? []);
    }

    private static string? ValidateAccess(
        ICurrentUser currentUser,
        string currency,
        ProductPricingWorkbenchView view)
    {
        if (!ProductPricingAccessRules.CanViewWorkbench(currentUser))
        {
            return "Only Sale, President or Developer can access the product pricing workbench.";
        }

        if (currentUser.CompanyId is not { } companyId || companyId == Guid.Empty)
        {
            return "Current company context is required.";
        }

        if (currency.Length is 0 or > QuotationRules.MaximumCurrencyLength)
        {
            return $"Currency is required and cannot exceed {QuotationRules.MaximumCurrencyLength} characters.";
        }

        return Enum.IsDefined(view) ? null : "View is invalid.";
    }

    private static OperationResult<PagedResult<ProductPricingWorkbenchItemDto>> EmptyResult(
        GetProductPricingWorkbenchQuery request)
        => OperationResult<PagedResult<ProductPricingWorkbenchItemDto>>.Ok(
            new PagedResult<ProductPricingWorkbenchItemDto>(
                [],
                0,
                request.NormalizedPageNumber,
                request.NormalizedPageSize));
}
