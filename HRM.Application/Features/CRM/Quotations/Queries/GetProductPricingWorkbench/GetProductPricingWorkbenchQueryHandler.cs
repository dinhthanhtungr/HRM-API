using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Commons.Rules;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Domain.Enums.Category;
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
    private readonly ICustomerVisibilityService _visibilityService;
    private readonly ProductPricingRealtimeSourceQueryService _sourceQueryService;
    private readonly ProductPricingRequestQueryService _requestQueryService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly QuotationFeatureOptions _featureOptions;

    public GetProductPricingWorkbenchQueryHandler(
        ICRMReadDbContext dbContext,
        ICurrentUser currentUser,
        ICustomerVisibilityService visibilityService,
        ProductPricingRealtimeSourceQueryService sourceQueryService,
        ProductPricingRequestQueryService requestQueryService,
        IDateTimeProvider dateTimeProvider,
        QuotationFeatureOptions featureOptions)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _visibilityService = visibilityService;
        _sourceQueryService = sourceQueryService;
        _requestQueryService = requestQueryService;
        _dateTimeProvider = dateTimeProvider;
        _featureOptions = featureOptions;
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
        }

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
            productIds: null,
            cancellationToken);
        var requestsByProduct = requests
            .GroupBy(x => x.ProductId)
            .ToDictionary(x => x.Key, x => (IReadOnlyList<ProductPricingRequestRow>)x.ToArray());

        // Tạm thời tắt nhánh tìm trực tiếp theo mã BBG để đối chiếu với luồng keyword cũ.
        // Khi bật lại, thay bằng lời gọi LoadQuotationKeywordProductIdsAsync bên dưới.
        var quotationKeywordProductIds = (IReadOnlySet<Guid>)new HashSet<Guid>();
        const bool isQuotationKeyword = false;
        // var quotationKeywordProductIds = await LoadQuotationKeywordProductIdsAsync(
        //     companyId,
        //     request.NormalizedKeyword,
        //     cancellationToken);
        // var isQuotationKeyword = IsQuotationExternalIdKeyword(request.NormalizedKeyword);

        var pendingRequestsByProduct = requestsByProduct
            .Where(x => !(versionsByProduct.GetValueOrDefault(x.Key) ?? [])
                .Any(version => version.Status == ProductPricingStatus.Approved))
            .ToDictionary(x => x.Key, x => x.Value);

        var filtered = products
            .Where(product => (isQuotationKeyword && quotationKeywordProductIds.Contains(product.ProductId))
                || MatchesView(
                request.View,
                product.HasEligiblePricingSource,
                versionsByProduct.GetValueOrDefault(product.ProductId) ?? [],
                pendingRequestsByProduct.ContainsKey(product.ProductId)))
            .Where(product => MatchesKeyword(
                product,
                versionsByProduct.GetValueOrDefault(product.ProductId) ?? [],
                requestsByProduct.GetValueOrDefault(product.ProductId) ?? [],
                quotationKeywordProductIds,
                request.NormalizedKeyword))
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
        var storedSources = await LoadSelectedSourcesAsync(
            storedSelections, companyId, request.NormalizedCurrency, cancellationToken);
        var productsWithoutStoredPricing = currentByProduct
            .Where(x => x.Value.Preferred is null)
            .Select(x => x.Key)
            .ToArray();
        var fallbackSources = await LoadFallbackSourcesAsync(
            productsWithoutStoredPricing, companyId, request.NormalizedCurrency, cancellationToken);
        var relatedCustomersByProduct = ProductPricingAccessRules.CanViewWorkbench(_currentUser)
            ? await LoadRelatedCustomersAsync(
                companyId,
                await _requestQueryService.LoadRelatedCustomersAsync(
                    companyId,
                    pageProductIds,
                    cancellationToken),
                cancellationToken)
            : new Dictionary<Guid, IReadOnlyList<ProductPricingWorkbenchCustomerContextDto>>();

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
                    _featureOptions);
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
                        now),
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
        if (!IsQuotationExternalIdKeyword(keyword))
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
            CancellationToken cancellationToken)
    {
        if (relatedCustomerRows.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<ProductPricingWorkbenchCustomerContextDto>>();
        }

        var customerIds = relatedCustomerRows.Select(x => x.CustomerId).Distinct().ToArray();
        var latestSummaries = await _dbContext.CustomerInteractionAiSummaries
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
        IReadOnlySet<Guid> quotationKeywordProductIds,
        string? keyword)
    {
        if (keyword is null)
        {
            return true;
        }

        return product.ProductCode.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
            product.ProductName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
            quotationKeywordProductIds.Contains(product.ProductId) ||
            versions.Any(x =>
                x.SourceExternalId?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true) ||
            requests.Any(x =>
                x.QuotationExternalId.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                x.CustomerExternalId.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                x.CustomerName.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsQuotationExternalIdKeyword(string? keyword)
        => keyword?.StartsWith(DocumentPrefix.BBG.ToString(), StringComparison.OrdinalIgnoreCase) == true;

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

        if (!string.Equals(
                currency,
                GetProductPricingWorkbenchQuery.StandardPricingCurrency,
                StringComparison.OrdinalIgnoreCase))
        {
            return "Product standard pricing is managed in VND only.";
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
