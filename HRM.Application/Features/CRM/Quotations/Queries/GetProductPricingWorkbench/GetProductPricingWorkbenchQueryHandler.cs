using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Commons.Rules;
using HRM.Application.Features.Pricing.Authorization;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.Products;
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
    private readonly ICustomerVisibilityService _visibilityService;
    private readonly ProductPricingRealtimeSourceQueryService _sourceQueryService;
    private readonly ProductPricingRequestQueryService _requestQueryService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly QuotationFeatureOptions _featureOptions;
    private readonly IPricingVisibilityService _pricingVisibilityService;
    private readonly StandardPriceRealtimeComparisonQueryService _comparisonQueryService;

    public GetProductPricingWorkbenchQueryHandler(
        ICRMReadDbContext dbContext,
        ICurrentUser currentUser,
        ICustomerVisibilityService visibilityService,
        ProductPricingRealtimeSourceQueryService sourceQueryService,
        ProductPricingRequestQueryService requestQueryService,
        IDateTimeProvider dateTimeProvider,
        QuotationFeatureOptions featureOptions,
        IPricingVisibilityService pricingVisibilityService,
        StandardPriceRealtimeComparisonQueryService comparisonQueryService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _visibilityService = visibilityService;
        _sourceQueryService = sourceQueryService;
        _requestQueryService = requestQueryService;
        _dateTimeProvider = dateTimeProvider;
        _featureOptions = featureOptions;
        _pricingVisibilityService = pricingVisibilityService;
        _comparisonQueryService = comparisonQueryService;
    }

    public async Task<OperationResult<PagedResult<ProductPricingWorkbenchItemDto>>> Handle(
        GetProductPricingWorkbenchQuery request,
        CancellationToken cancellationToken)
    {
        var pricingAccess = _pricingVisibilityService.GetAccess();
        var validationError = ValidateAccess(
            _currentUser,
            pricingAccess,
            request);
        if (validationError is not null)
        {
            return OperationResult<PagedResult<ProductPricingWorkbenchItemDto>>.Fail(
                validationError);
        }

        var canManagePricing = pricingAccess.CanManage;
        var companyId = _currentUser.CompanyId!.Value;
        var now = _dateTimeProvider.Now;
        var hasKeyword = request.NormalizedKeyword is not null;
        var productQuery = _dbContext.Products
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                x.CompanyId == companyId &&
                (hasKeyword ||
                 !x.SampleRequests.Any(sampleRequest =>
                     sampleRequest.IsActive &&
                     sampleRequest.CompanyId == companyId &&
                     sampleRequest.Customer.ExternalId ==
                          InternalCustomerRules.InternalCustomerExternalId)));
        var visibleSampleRequests = _dbContext.SampleRequests
            .AsNoTracking()
            .Where(sampleRequest =>
                sampleRequest.IsActive &&
                sampleRequest.CompanyId == companyId);

        if (!canManagePricing)
        {
            var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
            var visibleCustomerIds = _visibilityService
                .ApplyCustomerVisibility(_dbContext.Customers.AsNoTracking(), scope)
                .Select(customer => customer.CustomerId);

            productQuery = productQuery.Where(product =>
                product.SampleRequests.Any(sampleRequest =>
                    sampleRequest.IsActive &&
                    sampleRequest.CompanyId == companyId &&
                    visibleCustomerIds.Contains(sampleRequest.CustomerId)) ||
                _dbContext.QuotationLines.AsNoTracking().Any(line =>
                    line.IsActive &&
                    line.ProductId == product.ProductId &&
                    line.Quotation.IsActive &&
                    line.Quotation.CompanyId == companyId &&
                    visibleCustomerIds.Contains(line.Quotation.CustomerId)));
            visibleSampleRequests = visibleSampleRequests.Where(sampleRequest =>
                visibleCustomerIds.Contains(sampleRequest.CustomerId));
        }

        productQuery = ApplyFilters(productQuery, visibleSampleRequests, request);

        var quotationKeywordProductIds = request.NormalizedKeyword is { } &&
                                         request.EffectiveSearchType is
                                             ProductPricingWorkbenchSearchType.All or
                                             ProductPricingWorkbenchSearchType.Quotation
            ? await LoadQuotationKeywordProductIdsAsync(
                companyId,
                request.NormalizedKeyword,
                cancellationToken)
            : new HashSet<Guid>();
        productQuery = ApplyTypedKeywordFilter(
            productQuery,
            visibleSampleRequests,
            quotationKeywordProductIds,
            request);

        var sampleRequestKeywordProductIds = request.NormalizedKeyword is { } sampleRequestKeyword &&
                                             request.EffectiveSearchType ==
                                             ProductPricingWorkbenchSearchType.All
            ? (await visibleSampleRequests
                .Where(sampleRequest =>
                    EF.Functions.ILike(
                        sampleRequest.ExternalId,
                        PostgresSearchPattern.ContainsLiteral(sampleRequestKeyword),
                        PostgresSearchPattern.EscapeCharacter) ||
                    EF.Functions.ILike(
                        sampleRequest.Customer.ExternalId,
                        PostgresSearchPattern.ContainsLiteral(sampleRequestKeyword),
                        PostgresSearchPattern.EscapeCharacter) ||
                    EF.Functions.ILike(
                        sampleRequest.Customer.CustomerName,
                        PostgresSearchPattern.ContainsLiteral(sampleRequestKeyword),
                        PostgresSearchPattern.EscapeCharacter) ||
                    (sampleRequest.Formula != null && EF.Functions.ILike(
                        sampleRequest.Formula.ExternalId,
                        PostgresSearchPattern.ContainsLiteral(sampleRequestKeyword),
                        PostgresSearchPattern.EscapeCharacter)))
                .Select(sampleRequest => sampleRequest.ProductId)
                .Distinct()
                .ToListAsync(cancellationToken))
                .ToHashSet()
            : [];

        var products = await productQuery
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
            productIds,
            cancellationToken);
        var requestsByProduct = requests
            .GroupBy(x => x.ProductId)
            .ToDictionary(x => x.Key, x => (IReadOnlyList<ProductPricingRequestRow>)x.ToArray());
        var isQuotationKeyword = request.NormalizedKeyword is not null &&
                                 request.EffectiveSearchType ==
                                     ProductPricingWorkbenchSearchType.Quotation;

        var pendingRequestsByProduct = requestsByProduct
            .Select(x => new
            {
                x.Key,
                Requests = ProductPricingAttentionRules.GetPendingQuotationRequests(
                    Latest(
                        versionsByProduct.GetValueOrDefault(x.Key) ?? [],
                        ProductPricingStatus.Approved),
                    x.Value)
            })
            .Where(x => x.Requests.Count > 0)
            .ToDictionary(x => x.Key, x => x.Requests);
        IReadOnlyDictionary<Guid, DateTime> latestFormulaConfirmations =
            request.View == ProductPricingWorkbenchView.NeedsPricing
                ? await LoadLatestFormulaConfirmationsAsync(
                    productIds,
                    companyId,
                    cancellationToken)
                : new Dictionary<Guid, DateTime>();
        var materialCostChangedProductIds = await LoadMaterialCostViewProductIdsAsync(
            request.View,
            versionsByProduct,
            companyId,
            request.NormalizedCurrency,
            cancellationToken);
        var attentionSourcesByProduct = request.View == ProductPricingWorkbenchView.NeedsPricing
            ? productIds.ToDictionary(
                productId => productId,
                productId => ProductPricingAttentionRules.Resolve(
                    Latest(
                        versionsByProduct.GetValueOrDefault(productId) ?? [],
                        ProductPricingStatus.Approved),
                    requestsByProduct.GetValueOrDefault(productId) ?? [],
                    latestFormulaConfirmations.GetValueOrDefault(productId),
                    now,
                    _featureOptions,
                    materialCostChangedProductIds.Contains(productId)))
            : new Dictionary<Guid, IReadOnlyList<ProductPricingAttentionSource>>();

        var filtered = products
            .Where(product => (isQuotationKeyword && quotationKeywordProductIds.Contains(product.ProductId))
                || MatchesView(
                request.View,
                product.HasEligiblePricingSource,
                versionsByProduct.GetValueOrDefault(product.ProductId) ?? [],
                request.View == ProductPricingWorkbenchView.NeedsPricing
                    ? attentionSourcesByProduct[product.ProductId].Count > 0
                    : pendingRequestsByProduct.ContainsKey(product.ProductId),
                materialCostChangedProductIds))
            // Sale chỉ được thấy giá chuẩn đã được President/Developer duyệt.
            // Không dùng Draft hoặc giá realtime làm fallback cho visibility này.
            .Where(product => canManagePricing || HasApprovedStandardPrice(
                versionsByProduct.GetValueOrDefault(product.ProductId) ?? []))
            .Where(product => MatchesKeyword(
                product,
                versionsByProduct.GetValueOrDefault(product.ProductId) ?? [],
                requestsByProduct.GetValueOrDefault(product.ProductId) ?? [],
                quotationKeywordProductIds,
                sampleRequestKeywordProductIds,
                request.EffectiveSearchType == ProductPricingWorkbenchSearchType.All
                    ? request.NormalizedKeyword
                    : null))
            .ToArray();

        var ordered = ApplySorting(
            filtered,
            pendingRequestsByProduct,
            versionsByProduct,
            now,
            request);
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
            productId => ResolveVisibleVersions(
                versionsByProduct.GetValueOrDefault(productId) ?? [],
                canManagePricing));
        var storedSelections = currentByProduct.Values
            .Select(x => x.Preferred)
            .Where(x => x is not null && x.SourceId.HasValue && x.SourceType.HasValue)
            .Select(x => new ProductPricingSourceSelection(
                x!.ProductId,
                x.SourceType!.Value,
                x.SourceId!.Value))
            .Concat(currentByProduct.Values
                .Select(x => x.Approved)
                .Where(x => x is not null && x.SourceId.HasValue && x.SourceType.HasValue)
                .Select(x => new ProductPricingSourceSelection(
                    x!.ProductId,
                    x.SourceType!.Value,
                    x.SourceId!.Value)))
            .Distinct()
            .ToArray();
        var storedSources = await LoadSelectedSourcesAsync(
            storedSelections, companyId, request.NormalizedCurrency, cancellationToken);
        var productsWithoutStoredPricing = currentByProduct
            .Where(x => x.Value.Preferred is null)
            .Select(x => x.Key)
            .ToArray();
        var fallbackSources = await LoadFallbackSourcesAsync(
            productsWithoutStoredPricing, companyId, request.NormalizedCurrency, cancellationToken);
        var relatedCustomersByProduct = pricingAccess.CanViewWorkbench
            ? await LoadRelatedCustomersAsync(
                companyId,
                await _requestQueryService.LoadRelatedCustomersAsync(
                    companyId,
                    pageProductIds,
                cancellationToken),
                includeHealthSummary: pricingAccess.CanManage,
                cancellationToken: cancellationToken)
            : new Dictionary<Guid, IReadOnlyList<ProductPricingWorkbenchCustomerContextDto>>();
        if (request.View != ProductPricingWorkbenchView.NeedsPricing)
        {
            latestFormulaConfirmations = await LoadLatestFormulaConfirmationsAsync(
                pageProductIds,
                companyId,
                cancellationToken);
        }
        var comparisonRequests = currentByProduct.Values
                .Where(x => x.Approved is not null)
                .Select(x =>
                {
                    var approved = x.Approved!;
                    return new StandardPriceRealtimeComparisonRequest(
                        approved.ProductId,
                        request.NormalizedCurrency,
                        approved.StandardSellingPrice,
                        approved.MaterialCostSnapshot,
                        approved.SourceType,
                        approved.SourceId);
                })
                .ToArray();
        var comparisonsByProduct = _comparisonQueryService.BuildVisible(
            comparisonRequests,
            storedSources,
            pricingAccess);

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
                var health = ProductPricingHealthEvaluator.Evaluate(
                    source,
                    current.Draft,
                    current.Approved,
                    now,
                    _featureOptions,
                    latestFormulaConfirmations.GetValueOrDefault(product.ProductId));
                return ProductPricingWorkbenchVisibility.ApplyToSummary(
                    ProductPricingWorkbenchMapper.MapSummary(
                        product,
                        request.NormalizedCurrency,
                        current.Draft,
                        current.Approved,
                        source,
                        productRequests,
                        relatedCustomersByProduct.GetValueOrDefault(product.ProductId) ?? [],
                        health,
                        now,
                        comparisonsByProduct.GetValueOrDefault(product.ProductId)),
                    pricingAccess);
            })
            .ToArray();

        return OperationResult<PagedResult<ProductPricingWorkbenchItemDto>>.Ok(
            new PagedResult<ProductPricingWorkbenchItemDto>(
                items,
                totalCount,
                request.NormalizedPageNumber,
                request.NormalizedPageSize));
    }

    private static IQueryable<Product> ApplyFilters(
        IQueryable<Product> products,
        IQueryable<SampleRequest> visibleSampleRequests,
        GetProductPricingWorkbenchQuery request)
    {
        if (request.ProductId.HasValue)
        {
            products = products.Where(product => product.ProductId == request.ProductId.Value);
        }

        if (request.CategoryId.HasValue)
        {
            products = products.Where(product => product.CategoryId == request.CategoryId.Value);
        }

        if (GetProductPricingWorkbenchQuery.Normalize(request.Color) is { } color)
        {
            products = products.Where(product => product.ColourName == color);
        }

        if (GetProductPricingWorkbenchQuery.Normalize(request.AdditiveCode) is { } additiveCode)
        {
            products = products.Where(product => product.Additive == additiveCode);
        }

        var requestedStatuses = request.Status.HasValue
            ? (request.SampleStatuses ?? []).Append(request.Status.Value)
            : request.SampleStatuses ?? [];
        var statuses = requestedStatuses
            .Select(status => status.ToString())
            .Distinct()
            .ToArray();
        var hasSampleRequestFilter = statuses.Length > 0 ||
                                     request.FromDate.HasValue ||
                                     request.ToDate.HasValue ||
                                     request.CustomerId.HasValue ||
                                     request.SaleEmployeeId.HasValue;
        if (!hasSampleRequestFilter)
        {
            return products;
        }

        if (statuses.Length > 0)
        {
            visibleSampleRequests = visibleSampleRequests.Where(sampleRequest =>
                statuses.Contains(sampleRequest.Status));
        }

        if (request.FromDate.HasValue)
        {
            var from = request.FromDate.Value.Date;
            visibleSampleRequests = visibleSampleRequests.Where(sampleRequest =>
                sampleRequest.CreatedDate >= from);
        }

        if (request.ToDate.HasValue)
        {
            var toExclusive = request.ToDate.Value.Date.AddDays(1);
            visibleSampleRequests = visibleSampleRequests.Where(sampleRequest =>
                sampleRequest.CreatedDate < toExclusive);
        }

        if (request.CustomerId.HasValue)
        {
            visibleSampleRequests = visibleSampleRequests.Where(sampleRequest =>
                sampleRequest.CustomerId == request.CustomerId.Value);
        }

        if (request.SaleEmployeeId.HasValue)
        {
            visibleSampleRequests = visibleSampleRequests.Where(sampleRequest =>
                sampleRequest.ManagerBy == request.SaleEmployeeId.Value);
        }

        return products.Where(product => visibleSampleRequests.Any(sampleRequest =>
            sampleRequest.ProductId == product.ProductId));
    }

    private static IQueryable<Product> ApplyTypedKeywordFilter(
        IQueryable<Product> products,
        IQueryable<SampleRequest> visibleSampleRequests,
        IReadOnlySet<Guid> quotationKeywordProductIds,
        GetProductPricingWorkbenchQuery request)
    {
        if (request.NormalizedKeyword is not { } keyword ||
            request.EffectiveSearchType == ProductPricingWorkbenchSearchType.All)
        {
            return products;
        }

        var pattern = PostgresSearchPattern.PrefixLiteral(keyword);
        return request.EffectiveSearchType switch
        {
            ProductPricingWorkbenchSearchType.Quotation => products.Where(product =>
                quotationKeywordProductIds.Contains(product.ProductId)),
            ProductPricingWorkbenchSearchType.Customer => products.Where(product =>
                visibleSampleRequests.Any(sampleRequest =>
                    sampleRequest.ProductId == product.ProductId &&
                    EF.Functions.ILike(
                        sampleRequest.Customer.ExternalId,
                        pattern,
                        PostgresSearchPattern.EscapeCharacter))),
            ProductPricingWorkbenchSearchType.SampleRequest => products.Where(product =>
                visibleSampleRequests.Any(sampleRequest =>
                    sampleRequest.ProductId == product.ProductId &&
                    EF.Functions.ILike(
                        sampleRequest.ExternalId,
                        pattern,
                        PostgresSearchPattern.EscapeCharacter))),
            ProductPricingWorkbenchSearchType.Product => products.Where(product =>
                EF.Functions.ILike(
                    product.Code ?? string.Empty,
                    pattern,
                    PostgresSearchPattern.EscapeCharacter) ||
                EF.Functions.ILike(
                    product.ColourCode ?? string.Empty,
                    pattern,
                    PostgresSearchPattern.EscapeCharacter)),
            ProductPricingWorkbenchSearchType.Formula => products.Where(product =>
                visibleSampleRequests.Any(sampleRequest =>
                    sampleRequest.ProductId == product.ProductId &&
                    sampleRequest.Formula != null &&
                    EF.Functions.ILike(
                        sampleRequest.Formula.ExternalId,
                        pattern,
                        PostgresSearchPattern.EscapeCharacter))),
            _ => products.Where(_ => false)
        };
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
                PublisherNote = x.PublisherNote,
                Status = x.Status,
                Version = x.Version,
                ApprovedAt = x.ApprovedAt,
                PriceValidityDays = x.FormulaPricingPolicy != null
                    ? x.FormulaPricingPolicy.PriceValidityDays
                    : null,
                CreatedDate = x.CreatedDate,
                UpdatedDate = x.UpdatedDate
            })
            .ToListAsync(cancellationToken);

    private async Task<IReadOnlySet<Guid>> LoadQuotationKeywordProductIdsAsync(
        Guid companyId,
        string? keyword,
        CancellationToken cancellationToken)
    {
        if (keyword is null)
        {
            return new HashSet<Guid>();
        }

        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
        var productIds = await _visibilityService
            .ApplyQuotationVisibility(
                _dbContext.Quotations.AsNoTracking(),
                _dbContext.Customers.AsNoTracking(),
                scope)
            .Where(quotation =>
                quotation.CompanyId == companyId &&
                quotation.IsActive &&
                quotation.ExternalId.StartsWith(keyword!))
            .SelectMany(quotation => quotation.Lines
                .Where(line => line.IsActive)
                .Select(line => line.ProductId))
            .Distinct()
            .ToListAsync(cancellationToken);

        return productIds.ToHashSet();
    }

    private async Task<IReadOnlyDictionary<Guid, IReadOnlyList<ProductPricingWorkbenchCustomerContextDto>>>
        LoadRelatedCustomersAsync(
        Guid companyId,
        IReadOnlyList<ProductPricingRelatedCustomerRow> relatedCustomerRows,
        bool includeHealthSummary,
        CancellationToken cancellationToken)
    {
        if (relatedCustomerRows.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<ProductPricingWorkbenchCustomerContextDto>>();
        }

        List<CustomerHealthSummaryRow> latestSummaries = [];
        if (includeHealthSummary)
        {
            var customerIds = relatedCustomerRows.Select(x => x.CustomerId).Distinct().ToArray();
            latestSummaries = await _dbContext.CustomerInteractionAiSummaries
                .AsNoTracking()
                .Where(x =>
                    x.CompanyId == companyId &&
                    x.IsActive &&
                    x.IsAiSuccess &&
                    customerIds.Contains(x.CustomerId))
                .OrderByDescending(x => x.AiGeneratedDate ?? x.UpdatedDate ?? x.CreatedDate)
                .ThenByDescending(x => x.CreatedDate)
                .Select(x => new CustomerHealthSummaryRow
                {
                    CustomerId = x.CustomerId,
                    Summary = x.Summary,
                    CustomerNeed = x.CustomerNeed,
                    CurrentStage = x.CurrentStage,
                    Risk = x.Risk,
                    NextAction = x.NextAction,
                    Sentiment = x.Sentiment,
                    GeneratedAt = x.AiGeneratedDate ?? x.UpdatedDate ?? x.CreatedDate
                })
                .ToListAsync(cancellationToken);
        }

        var summaryByCustomer = latestSummaries
            .GroupBy(x => x.CustomerId)
            .ToDictionary(x => x.Key, x => x.First());

        return relatedCustomerRows
            .GroupBy(x => x.ProductId)
            .ToDictionary(
                product => product.Key,
                product => (IReadOnlyList<ProductPricingWorkbenchCustomerContextDto>)product
                    .GroupBy(x => new { x.CustomerId, x.CustomerExternalId, x.CustomerName })
                    .Select(customer =>
                    {
                        var latestRelatedDate = customer.Max(x => x.RelatedDate);
                        var summary = summaryByCustomer.GetValueOrDefault(customer.Key.CustomerId);
                        return new ProductPricingWorkbenchCustomerContextDto
                        {
                            CustomerId = customer.Key.CustomerId,
                            CustomerCode = customer.Key.CustomerExternalId,
                            CustomerName = customer.Key.CustomerName,
                            RelatedDocumentCount = customer
                                .Select(x => x.RelatedDocumentId)
                                .Distinct()
                                .Count(),
                            LatestRelatedDate = latestRelatedDate,
                            HealthSummary = summary is null
                                ? null
                                : new ProductPricingWorkbenchCustomerHealthSummaryDto
                                {
                                    Summary = summary.Summary,
                                    CustomerNeed = summary.CustomerNeed,
                                    CurrentStage = summary.CurrentStage,
                                    Risk = summary.Risk,
                                    NextAction = summary.NextAction,
                                    Sentiment = summary.Sentiment,
                                    GeneratedAt = summary.GeneratedAt
                                }
                        };
                    })
                    .OrderByDescending(x => x.LatestRelatedDate)
                    .ThenBy(x => x.CustomerName)
                    .ToArray());
    }

    private async Task<IReadOnlyDictionary<ProductPricingSourceSelection, ProductPricingSourceOptionDto>>
        LoadSelectedSourcesAsync(
            IReadOnlyCollection<ProductPricingSourceSelection> selections,
            Guid companyId,
            string currency,
            CancellationToken cancellationToken)
    {
        return await _sourceQueryService.LoadSelectedAsync(
            selections, companyId, currency, cancellationToken);
    }

    private async Task<IReadOnlyDictionary<Guid, IReadOnlyList<ProductPricingSourceOptionDto>>>
        LoadFallbackSourcesAsync(
            IReadOnlyCollection<Guid> productIds,
            Guid companyId,
            string currency,
            CancellationToken cancellationToken)
    {
        return await _sourceQueryService.LoadAsync(
            productIds, companyId, currency, cancellationToken);
    }

    private static bool MatchesView(
        ProductPricingWorkbenchView view,
        bool hasEligiblePricingSource,
        IReadOnlyList<PricingVersionRow> versions,
        bool needsPricingAttention,
        IReadOnlySet<Guid> materialCostChangedProductIds)
    {
        var hasDraft = versions.Any(x => x.Status == ProductPricingStatus.Draft);
        var hasApproved = versions.Any(x => x.Status == ProductPricingStatus.Approved);
        return view switch
        {
            ProductPricingWorkbenchView.NeedsPricing => needsPricingAttention,
            ProductPricingWorkbenchView.Draft => hasDraft,
            ProductPricingWorkbenchView.Approved => hasApproved,
            ProductPricingWorkbenchView.MaterialCostChanged =>
                materialCostChangedProductIds.Contains(versions.FirstOrDefault()?.ProductId ?? Guid.Empty),
            ProductPricingWorkbenchView.ProductionMaterialCostChanged =>
                materialCostChangedProductIds.Contains(versions.FirstOrDefault()?.ProductId ?? Guid.Empty),
            ProductPricingWorkbenchView.All =>
                needsPricingAttention || versions.Count > 0 || hasEligiblePricingSource,
            _ => false
        };
    }

    private async Task<IReadOnlySet<Guid>> LoadMaterialCostViewProductIdsAsync(
        ProductPricingWorkbenchView view,
        IReadOnlyDictionary<Guid, IReadOnlyList<PricingVersionRow>> versionsByProduct,
        Guid companyId,
        string currency,
        CancellationToken cancellationToken)
    {
        var useLatestProductionFormula = view ==
            ProductPricingWorkbenchView.ProductionMaterialCostChanged;
        if (view != ProductPricingWorkbenchView.NeedsPricing &&
            view != ProductPricingWorkbenchView.MaterialCostChanged &&
            !useLatestProductionFormula)
        {
            return new HashSet<Guid>();
        }

        var baselines = versionsByProduct
            .Select(pair => Latest(pair.Value, ProductPricingStatus.Approved))
            .Where(x => x?.MaterialCostSnapshot is > 0m)
            .Select(x => new ProductPricingMaterialCostBaseline(
                x!.ProductId,
                x.MaterialCostSnapshot!.Value,
                x.SourceType.HasValue && x.SourceId.HasValue
                    ? new ProductPricingSourceSelection(
                        x.ProductId,
                        x.SourceType.Value,
                        x.SourceId.Value)
                    : null))
            .ToArray();
        return await _sourceQueryService.LoadMaterialCostChangedProductIdsAsync(
            baselines,
            companyId,
            currency,
            view == ProductPricingWorkbenchView.MaterialCostChanged
                ? 0m
                : _featureOptions.MaterialCostChangeThresholdPercent,
            useLatestProductionFormula,
            cancellationToken);
    }

    private static bool MatchesKeyword(
        ProductRow product,
        IReadOnlyList<PricingVersionRow> versions,
        IReadOnlyList<ProductPricingRequestRow> requests,
        IReadOnlySet<Guid> quotationKeywordProductIds,
        IReadOnlySet<Guid> sampleRequestKeywordProductIds,
        string? keyword)
    {
        if (keyword is null)
        {
            return true;
        }

        return product.ProductCode.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
            product.ProductName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
            quotationKeywordProductIds.Contains(product.ProductId) ||
            sampleRequestKeywordProductIds.Contains(product.ProductId) ||
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
        IReadOnlyDictionary<Guid, IReadOnlyList<PricingVersionRow>> versionsByProduct,
        DateTime now,
        GetProductPricingWorkbenchQuery request)
    {
        Func<ProductRow, DateTime?> latestRequestedAt = product =>
            requestsByProduct
                .GetValueOrDefault(product.ProductId)?
                .Max(x => x.RequestedAt);
        Func<ProductRow, int> attentionRank = product => GetAttentionRank(
            product,
            requestsByProduct,
            versionsByProduct,
            now);
        Func<ProductRow, DateTime?> priceExpiresAt = product => GetApprovedPriceExpiresAt(
            versionsByProduct.GetValueOrDefault(product.ProductId) ?? []);

        // Báo giá đang chờ luôn cần xử lý trước; tiếp theo là giá chuẩn đã quá hạn
        // hoặc gần hết hạn. Các sort FE gửi lên chỉ sắp trong từng nhóm ưu tiên này.
        var prioritized = products
            .OrderBy(attentionRank)
            .ThenBy(priceExpiresAt);

        return request.NormalizedSortBy?.ToLowerInvariant() switch
        {
            GetProductPricingWorkbenchSortFields.ProductCode =>
                (request.SortDescending
                    ? prioritized.ThenByDescending(x => x.ProductCode)
                    : prioritized.ThenBy(x => x.ProductCode))
                .ThenByDescending(x => latestRequestedAt(x))
                .ThenByDescending(x => x.LatestSampleRequestCreatedDate)
                .ThenBy(x => x.ProductId)
                .ToArray(),

            GetProductPricingWorkbenchSortFields.ProductName =>
                (request.SortDescending
                    ? prioritized.ThenByDescending(x => x.ProductName)
                    : prioritized.ThenBy(x => x.ProductName))
                .ThenByDescending(x => latestRequestedAt(x))
                .ThenByDescending(x => x.LatestSampleRequestCreatedDate)
                .ThenBy(x => x.ProductId)
                .ToArray(),

            GetProductPricingWorkbenchSortFields.RequestedAt =>
                (request.SortDescending
                    ? prioritized.ThenByDescending(x => latestRequestedAt(x))
                    : prioritized.ThenBy(x => latestRequestedAt(x)))
                .ThenByDescending(x => x.LatestSampleRequestCreatedDate)
                .ThenBy(x => x.ProductCode)
                .ThenBy(x => x.ProductId)
                .ToArray(),

            GetProductPricingWorkbenchSortFields.CreatedDate =>
                (request.SortDescending
                    ? prioritized.ThenByDescending(x => x.CreatedDate)
                    : prioritized.ThenBy(x => x.CreatedDate))
                .ThenByDescending(x => x.UpdatedDate)
                .ThenByDescending(x => x.ProductId)
                .ToArray(),

            _ => prioritized
                .ThenByDescending(x => latestRequestedAt(x))
                .ThenByDescending(x => x.LatestSampleRequestCreatedDate)
                .ThenByDescending(x => x.UpdatedDate ?? x.CreatedDate)
                .ThenBy(x => x.ProductCode)
                .ThenBy(x => x.ProductId)
                .ToArray()
        };
    }

    private static int GetAttentionRank(
        ProductRow product,
        IReadOnlyDictionary<Guid, IReadOnlyList<ProductPricingRequestRow>> requestsByProduct,
        IReadOnlyDictionary<Guid, IReadOnlyList<PricingVersionRow>> versionsByProduct,
        DateTime now)
    {
        if (requestsByProduct.ContainsKey(product.ProductId))
        {
            return 0;
        }

        var expiresAt = GetApprovedPriceExpiresAt(
            versionsByProduct.GetValueOrDefault(product.ProductId) ?? []);
        return expiresAt?.Date < now.Date ? 1 :
            expiresAt.HasValue ? 2 : 3;
    }

    private static DateTime? GetApprovedPriceExpiresAt(
        IReadOnlyList<PricingVersionRow> versions)
    {
        var approved = Latest(versions, ProductPricingStatus.Approved);
        return approved?.ApprovedAt.HasValue == true && approved.PriceValidityDays is > 0
            ? approved.ApprovedAt.Value.AddDays(approved.PriceValidityDays.Value)
            : null;
    }

    private static CurrentPricingRows ResolveCurrentVersions(
        IReadOnlyList<PricingVersionRow> versions)
    {
        var draft = Latest(versions, ProductPricingStatus.Draft);
        var approved = Latest(versions, ProductPricingStatus.Approved);
        return new CurrentPricingRows(draft, approved);
    }

    private async Task<IReadOnlyDictionary<Guid, DateTime>> LoadLatestFormulaConfirmationsAsync(
        IReadOnlyCollection<Guid> productIds,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var rows = await _dbContext.Formulas.AsNoTracking()
            .Where(x =>
                x.CompanyId == companyId &&
                x.IsActive &&
                productIds.Contains(x.ProductId) &&
                x.CheckDate.HasValue &&
                x.Status != FormulaStatus.Cancelled.ToString() &&
                x.Status != FormulaStatus.Rejected.ToString())
            .GroupBy(x => x.ProductId)
            .Select(x => new { ProductId = x.Key, ConfirmedAt = x.Max(y => y.CheckDate) })
            .ToListAsync(cancellationToken);

        return rows
            .Where(x => x.ConfirmedAt.HasValue)
            .ToDictionary(x => x.ProductId, x => x.ConfirmedAt!.Value);
    }

    private static CurrentPricingRows ResolveVisibleVersions(
        IReadOnlyList<PricingVersionRow> versions,
        bool canManagePricing)
    {
        var current = ResolveCurrentVersions(versions);
        return canManagePricing
            ? current
            : new CurrentPricingRows(null, current.Approved);
    }

    private static bool HasApprovedStandardPrice(
        IReadOnlyList<PricingVersionRow> versions)
        => Latest(versions, ProductPricingStatus.Approved)?.StandardSellingPrice > 0m;

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
        PricingAccessDecision pricingAccess,
        GetProductPricingWorkbenchQuery request)
    {
        if (!pricingAccess.CanViewWorkbench)
        {
            return "Only Sale, President or Developer can access the product pricing workbench.";
        }

        if (currentUser.CompanyId is not { } companyId || companyId == Guid.Empty)
        {
            return "Current company context is required.";
        }

        if (request.NormalizedCurrency.Length is 0 or > QuotationRules.MaximumCurrencyLength)
        {
            return $"Currency is required and cannot exceed {QuotationRules.MaximumCurrencyLength} characters.";
        }

        if (!string.Equals(
                request.NormalizedCurrency,
                GetProductPricingWorkbenchQuery.StandardPricingCurrency,
                StringComparison.OrdinalIgnoreCase))
        {
            return "Product standard pricing is managed in VND only.";
        }

        if (!Enum.IsDefined(request.View))
        {
            return "View is invalid.";
        }

        if (request.SearchType.HasValue && !Enum.IsDefined(request.SearchType.Value))
        {
            return "searchType is invalid.";
        }

        if ((request.SampleStatuses ?? []).Any(status => !Enum.IsDefined(status)) ||
            (request.Status.HasValue && !Enum.IsDefined(request.Status.Value)))
        {
            return "sampleStatuses contains an invalid value.";
        }

        if (request.FromDate.HasValue && request.ToDate.HasValue &&
            request.FromDate.Value.Date > request.ToDate.Value.Date)
        {
            return "fromDate cannot be later than toDate.";
        }

        return null;
    }

    private static OperationResult<PagedResult<ProductPricingWorkbenchItemDto>> EmptyResult(
        GetProductPricingWorkbenchQuery request)
        => OperationResult<PagedResult<ProductPricingWorkbenchItemDto>>.Ok(
            new PagedResult<ProductPricingWorkbenchItemDto>(
                [],
                0,
                request.NormalizedPageNumber,
                request.NormalizedPageSize));

    private sealed class CustomerHealthSummaryRow
    {
        public Guid CustomerId { get; init; }
        public string Summary { get; init; } = string.Empty;
        public string CustomerNeed { get; init; } = string.Empty;
        public string CurrentStage { get; init; } = string.Empty;
        public string Risk { get; init; } = string.Empty;
        public string NextAction { get; init; } = string.Empty;
        public string Sentiment { get; init; } = string.Empty;
        public DateTime? GeneratedAt { get; init; }
    }
}
