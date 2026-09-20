using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.Executive;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Commons.Pricing.Helpers;
using HRM.Application.Commons.Rules;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Application.Features.Executive.SampleRequestPricingOverview.Dtos;
using HRM.Application.Features.Executive.SampleRequestPricingOverview.Models;
using HRM.Application.Features.InternalMail.Services;
using HRM.Application.Features.Pricing.Authorization;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.InternalMailEnums;
using HRM.Domain.Enums.Products;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Executive.SampleRequestPricingOverview;

/// <summary>
/// Builds one President/Developer list card per Sample Request, enriching only the
/// database-paged products with the canonical pricing and Internal Mail projections.
/// </summary>
internal sealed class GetSampleRequestPricingOverviewQueryHandler
    : IRequestHandler<
        GetSampleRequestPricingOverviewQuery,
        OperationResult<PagedResult<SampleRequestPricingOverviewItemDto>>>
{
    private static readonly string[] SupportedSortFields =
    [
        "latestActivityAt", "createdDate", "updatedDate", "requestCode", "externalId",
        "productCode", "productName", "colourCode", "customerName"
    ];
    private const string QuotationRequestedPayload =
        """{"contentType":"QuotationRequested"}""";
    private const string QuotationRequestWithdrawnPayload =
        """{"contentType":"QuotationPricingRequestWithdrawn"}""";

    private readonly IExecutiveReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly ICustomerVisibilityService _visibilityService;
    private readonly ProductPricingRealtimeSourceQueryService _sourceQueryService;
    private readonly ProductPricingRequestQueryService _requestQueryService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly QuotationFeatureOptions _featureOptions;
    private readonly IInternalConversationAccessService _conversationAccessService;
    private readonly IPricingVisibilityService _pricingVisibilityService;
    private readonly StandardPriceRealtimeComparisonQueryService _comparisonQueryService;

    public GetSampleRequestPricingOverviewQueryHandler(
        IExecutiveReadDbContext dbContext,
        ICurrentUser currentUser,
        ICustomerVisibilityService visibilityService,
        ProductPricingRealtimeSourceQueryService sourceQueryService,
        ProductPricingRequestQueryService requestQueryService,
        IDateTimeProvider dateTimeProvider,
        QuotationFeatureOptions featureOptions,
        IInternalConversationAccessService conversationAccessService,
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
        _conversationAccessService = conversationAccessService;
        _pricingVisibilityService = pricingVisibilityService;
        _comparisonQueryService = comparisonQueryService;
    }

    public async Task<OperationResult<PagedResult<SampleRequestPricingOverviewItemDto>>> Handle(
        GetSampleRequestPricingOverviewQuery request,
        CancellationToken cancellationToken)
    {
        var validationError = Validate(request);
        if (validationError is not null)
        {
            return OperationResult<PagedResult<SampleRequestPricingOverviewItemDto>>.Fail(validationError);
        }

        var companyId = _currentUser.CompanyId!.Value;
        var employeeId = _currentUser.EmployeeId!.Value;
        var pricingAccess = _pricingVisibilityService.GetAccess();
        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
        var visibilityScope = request.NormalizedKeyword is not null
            ? scope with { CanViewInternalCustomer = true }
            : scope;
        var sampleRequests = _visibilityService.ApplySampleRequestVisibility(
                _dbContext.SampleRequests.AsNoTracking().Where(x => x.IsActive),
                _dbContext.Customers.AsNoTracking(),
                visibilityScope)
            .Where(x =>
                x.CompanyId == companyId &&
                x.Customer.CompanyId == companyId &&
                x.Product.CompanyId == companyId &&
                x.Product.IsActive);

        sampleRequests = ApplyFilters(
            sampleRequests,
            _dbContext.QuotationLines.AsNoTracking(),
            request,
            includeKeyword: false);
        sampleRequests = await ApplyKeywordFilterAsync(
            sampleRequests,
            _dbContext.QuotationLines.AsNoTracking(),
            request.NormalizedKeyword,
            request.EffectiveSearchType,
            companyId,
            cancellationToken);
        sampleRequests = await ApplyPricingViewAsync(
            sampleRequests,
            request.View,
            companyId,
            request.NormalizedCurrency,
            cancellationToken);
        var totalCount = await sampleRequests.CountAsync(cancellationToken);
        var projected = Project(
            sampleRequests,
            companyId,
            request.NormalizedCurrency,
            includeActivitySubqueries: string.Equals(
                request.NormalizedSortBy,
                "latestActivityAt",
                StringComparison.OrdinalIgnoreCase));
        var pageRows = await ApplySorting(projected, request)
            .Skip((request.NormalizedPageNumber - 1) * request.NormalizedPageSize)
            .Take(request.NormalizedPageSize)
            .ToListAsync(cancellationToken);

        if (pageRows.Count == 0)
        {
            return Ok([], totalCount, request);
        }

        var productIds = pageRows.Select(x => x.ProductId).Distinct().ToArray();
        var sampleRequestIds = pageRows.Select(x => x.SampleRequestId).ToArray();
        var pricing = await LoadPricingAsync(
            productIds,
            companyId,
            request.NormalizedCurrency,
            pricingAccess,
            cancellationToken);
        var conversations = await LoadConversationsAsync(
            sampleRequestIds,
            companyId,
            employeeId,
            _conversationAccessService.CanReadExecutiveSampleRequestConversations,
            cancellationToken);

        var items = pageRows.Select(row => MapItem(
            row,
            pricing.ByProduct.GetValueOrDefault(row.ProductId),
            conversations.GetValueOrDefault(row.SampleRequestId),
            pricing.MaterialsByProduct.GetValueOrDefault(row.ProductId)))
            .ToArray();

        return Ok(items, totalCount, request);
    }

    private IQueryable<SampleRequestOverviewRow> Project(
        IQueryable<SampleRequest> query,
        Guid companyId,
        string currency,
        bool includeActivitySubqueries)
        => includeActivitySubqueries
            ? ProjectWithActivity(query, companyId, currency)
            : ProjectWithoutActivity(query, companyId);

    private IQueryable<SampleRequestOverviewRow> ProjectWithActivity(
        IQueryable<SampleRequest> query,
        Guid companyId,
        string currency)
        => from x in query
           let sampleActivityAt = x.UpdatedDate ?? x.CreatedDate
           let pricingActivityAt = _dbContext.ProductPricingVersions
               .Where(version =>
                   version.CompanyId == companyId &&
                   version.ProductId == x.ProductId &&
                   version.Currency == currency &&
                   version.IsActive &&
                   (version.Status == ProductPricingStatus.Draft ||
                    version.Status == ProductPricingStatus.Approved))
               .Max(version => (DateTime?)(version.UpdatedDate ?? version.CreatedDate))
           let conversationActivityAt = _dbContext.InternalConversations
               .Where(conversation =>
                   conversation.CompanyId == companyId &&
                   conversation.IsActive &&
                   conversation.RelatedType == InternalMailRelatedType.SampleRequest &&
                   conversation.RelatedId == x.SampleRequestId)
               .Max(conversation => (DateTime?)conversation.LastMessageAt)
           let latestActivityAt = conversationActivityAt.HasValue &&
                                  conversationActivityAt.Value > sampleActivityAt &&
                                  (!pricingActivityAt.HasValue || conversationActivityAt.Value > pricingActivityAt.Value)
               ? conversationActivityAt.Value
               : pricingActivityAt.HasValue && pricingActivityAt.Value > sampleActivityAt
                   ? pricingActivityAt.Value
                   : sampleActivityAt
           select new SampleRequestOverviewRow
           {
               SampleRequestId = x.SampleRequestId,
               ProductId = x.ProductId,
               RequestCode = x.ExternalId,
               CreatedDate = x.CreatedDate,
               UpdatedDate = x.UpdatedDate,
               Status = x.Status,
               ProductCode = x.Product.Code ?? string.Empty,
               ProductName = x.Product.Name ?? string.Empty,
               ColourCode = x.Product.ColourCode,
               ColourName = x.Product.ColourName,
               CategoryId = x.Product.CategoryId,
               CategoryCode = x.Product.Category != null ? x.Product.Category!.ExternalId : null,
               CategoryName = x.Product.Category != null ? x.Product.Category!.Name : null,
               AdditiveCode = x.Product.Additive,
               LabEmployeeId = x.Product.CreatedByNavigation != null &&
                               x.Product.CreatedByNavigation!.CompanyId == companyId
                   ? x.Product.CreatedBy
                   : null,
               LabName = x.Product.CreatedByNavigation != null &&
                         x.Product.CreatedByNavigation!.CompanyId == companyId
                   ? x.Product.CreatedByNavigation!.FullName
                   : null,
               CustomerId = x.CustomerId,
               CustomerCode = x.Customer.ExternalId,
               CustomerName = x.Customer.CustomerName,
               SaleEmployeeId = x.ManagerByNavigation.CompanyId == companyId
                   ? x.ManagerBy
                   : null,
               SaleName = x.ManagerByNavigation.CompanyId == companyId
                   ? x.ManagerByNavigation.FullName
                   : null,
               RequestedDeliveryDate = x.RequestDeliveryDate,
               ExpectedDeliveryDate = x.ExpectedDeliveryDate,
               PricingActivityAt = pricingActivityAt,
               ConversationActivityAt = conversationActivityAt,
               LatestActivityAt = latestActivityAt
           };

    private static IQueryable<SampleRequestOverviewRow> ProjectWithoutActivity(
        IQueryable<SampleRequest> query,
        Guid companyId)
        => from x in query
           let sampleActivityAt = x.UpdatedDate ?? x.CreatedDate
           select new SampleRequestOverviewRow
           {
               SampleRequestId = x.SampleRequestId,
               ProductId = x.ProductId,
               RequestCode = x.ExternalId,
               CreatedDate = x.CreatedDate,
               UpdatedDate = x.UpdatedDate,
               Status = x.Status,
               ProductCode = x.Product.Code ?? string.Empty,
               ProductName = x.Product.Name ?? string.Empty,
               ColourCode = x.Product.ColourCode,
               ColourName = x.Product.ColourName,
               CategoryId = x.Product.CategoryId,
               CategoryCode = x.Product.Category != null ? x.Product.Category!.ExternalId : null,
               CategoryName = x.Product.Category != null ? x.Product.Category!.Name : null,
               AdditiveCode = x.Product.Additive,
               LabEmployeeId = x.Product.CreatedByNavigation != null &&
                               x.Product.CreatedByNavigation!.CompanyId == companyId
                   ? x.Product.CreatedBy
                   : null,
               LabName = x.Product.CreatedByNavigation != null &&
                         x.Product.CreatedByNavigation!.CompanyId == companyId
                   ? x.Product.CreatedByNavigation!.FullName
                   : null,
               CustomerId = x.CustomerId,
               CustomerCode = x.Customer.ExternalId,
               CustomerName = x.Customer.CustomerName,
               SaleEmployeeId = x.ManagerByNavigation.CompanyId == companyId
                   ? x.ManagerBy
                   : null,
               SaleName = x.ManagerByNavigation.CompanyId == companyId
                   ? x.ManagerByNavigation.FullName
                   : null,
               RequestedDeliveryDate = x.RequestDeliveryDate,
               ExpectedDeliveryDate = x.ExpectedDeliveryDate,
               LatestActivityAt = sampleActivityAt
           };

    internal static IQueryable<SampleRequest> ApplyFilters(
        IQueryable<SampleRequest> query,
        IQueryable<QuotationLine> quotationLines,
        GetSampleRequestPricingOverviewQuery request,
        bool includeKeyword = true)
    {
        if (includeKeyword && request.NormalizedKeyword is { } keyword)
        {
            query = ApplyBroadKeywordFilter(query, quotationLines, keyword);
        }

        var requestedStatuses = request.Status.HasValue
            ? request.SampleStatuses.Append(request.Status.Value)
            : request.SampleStatuses;
        var statuses = requestedStatuses
            .Select(x => x.ToString())
            .Distinct()
            .ToArray();
        if (statuses.Length > 0)
        {
            query = query.Where(x => statuses.Contains(x.Status));
        }

        if (request.FromDate.HasValue)
        {
            var from = request.FromDate.Value.Date;
            query = query.Where(x => x.CreatedDate >= from);
        }

        if (request.ToDate.HasValue)
        {
            var toExclusive = request.ToDate.Value.Date.AddDays(1);
            query = query.Where(x => x.CreatedDate < toExclusive);
        }

        if (request.CustomerId.HasValue)
            query = query.Where(x => x.CustomerId == request.CustomerId.Value);
        if (request.ProductId.HasValue)
            query = query.Where(x => x.ProductId == request.ProductId.Value);
        if (request.SaleEmployeeId.HasValue)
            query = query.Where(x => x.ManagerBy == request.SaleEmployeeId.Value);
        if (request.CategoryId.HasValue)
            query = query.Where(x => x.Product.CategoryId == request.CategoryId.Value);
        if (GetSampleRequestPricingOverviewQuery.Normalize(request.Color) is { } color)
            query = query.Where(x => x.Product.ColourName == color);
        if (GetSampleRequestPricingOverviewQuery.Normalize(request.AdditiveCode) is { } additive)
            query = query.Where(x => x.Product.Additive == additive);

        return query;
    }

    private async Task<IQueryable<SampleRequest>> ApplyKeywordFilterAsync(
        IQueryable<SampleRequest> query,
        IQueryable<QuotationLine> quotationLines,
        string? keyword,
        SampleRequestPricingOverviewSearchType searchType,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        if (keyword is null)
        {
            return query;
        }

        if (searchType != SampleRequestPricingOverviewSearchType.All)
        {
            return searchType switch
            {
                SampleRequestPricingOverviewSearchType.Quotation => await ApplyQuotationKeywordFilterAsync(
                    query, quotationLines, keyword, companyId, cancellationToken),
                SampleRequestPricingOverviewSearchType.Customer => query.Where(x =>
                    EF.Functions.ILike(
                        x.Customer.ExternalId,
                        PostgresSearchPattern.PrefixLiteral(keyword),
                        PostgresSearchPattern.EscapeCharacter)),
                SampleRequestPricingOverviewSearchType.SampleRequest => query.Where(x =>
                    EF.Functions.ILike(
                        x.ExternalId,
                        PostgresSearchPattern.PrefixLiteral(keyword),
                        PostgresSearchPattern.EscapeCharacter)),
                SampleRequestPricingOverviewSearchType.Product => query.Where(x =>
                    EF.Functions.ILike(
                        x.Product.Code ?? string.Empty,
                        PostgresSearchPattern.PrefixLiteral(keyword),
                        PostgresSearchPattern.EscapeCharacter) ||
                    EF.Functions.ILike(
                        x.Product.ColourCode ?? string.Empty,
                        PostgresSearchPattern.PrefixLiteral(keyword),
                        PostgresSearchPattern.EscapeCharacter)),
                SampleRequestPricingOverviewSearchType.Formula => query.Where(x =>
                    x.Formula != null && EF.Functions.ILike(
                        x.Formula.ExternalId,
                        PostgresSearchPattern.PrefixLiteral(keyword),
                        PostgresSearchPattern.EscapeCharacter)),
                _ => query.Where(_ => false)
            };
        }

        // Codes such as TL41179C are the common Executive lookup path. If one of the
        // canonical identifiers matches exactly, avoid the expensive broad text/quotation scan.
        if (IsIdentifierKeyword(keyword))
        {
            var exactMatches = ApplyExactIdentifierFilter(query, keyword);
            if (await exactMatches.AnyAsync(cancellationToken))
            {
                return exactMatches;
            }
        }

        return ApplyBroadKeywordFilter(query, quotationLines, keyword);
    }

    private static async Task<IQueryable<SampleRequest>> ApplyQuotationKeywordFilterAsync(
        IQueryable<SampleRequest> query,
        IQueryable<QuotationLine> quotationLines,
        string keyword,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var quotationIds = await quotationLines
            .Where(line =>
                line.IsActive &&
                line.Quotation.IsActive &&
                line.Quotation.CompanyId == companyId &&
                EF.Functions.ILike(
                    line.Quotation.ExternalId,
                    PostgresSearchPattern.PrefixLiteral(keyword),
                    PostgresSearchPattern.EscapeCharacter))
            .Select(line => line.QuotationId)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        if (quotationIds.Length == 0)
        {
            return query.Where(_ => false);
        }

        return query.Where(x => quotationLines.Any(line =>
            line.IsActive &&
            quotationIds.Contains(line.QuotationId) &&
            line.Quotation.IsActive &&
            line.Quotation.CompanyId == x.CompanyId &&
            line.Quotation.CustomerId == x.CustomerId &&
            (line.SampleRequestId == x.SampleRequestId ||
             (line.SampleRequestId == null && line.ProductId == x.ProductId))));
    }

    private static IQueryable<SampleRequest> ApplyExactIdentifierFilter(
        IQueryable<SampleRequest> query,
        string keyword)
    {
        var pattern = PostgresSearchPattern.ExactLiteral(keyword);
        return query.Where(x =>
            EF.Functions.ILike(x.ExternalId, pattern, PostgresSearchPattern.EscapeCharacter) ||
            EF.Functions.ILike(x.Product.Code ?? string.Empty, pattern, PostgresSearchPattern.EscapeCharacter) ||
            EF.Functions.ILike(x.Product.ColourCode ?? string.Empty, pattern, PostgresSearchPattern.EscapeCharacter) ||
            EF.Functions.ILike(x.Customer.ExternalId, pattern, PostgresSearchPattern.EscapeCharacter) ||
            (x.Formula != null && EF.Functions.ILike(
                x.Formula.ExternalId,
                pattern,
                PostgresSearchPattern.EscapeCharacter)));
    }

    private static IQueryable<SampleRequest> ApplyBroadKeywordFilter(
        IQueryable<SampleRequest> query,
        IQueryable<QuotationLine> quotationLines,
        string keyword)
    {
        var pattern = PostgresSearchPattern.ContainsLiteral(keyword);
        return query.Where(x =>
            EF.Functions.ILike(x.ExternalId, pattern, PostgresSearchPattern.EscapeCharacter) ||
            EF.Functions.ILike(x.Product.Code ?? string.Empty, pattern, PostgresSearchPattern.EscapeCharacter) ||
            EF.Functions.ILike(x.Product.Name ?? string.Empty, pattern, PostgresSearchPattern.EscapeCharacter) ||
            EF.Functions.ILike(x.Product.ColourCode ?? string.Empty, pattern, PostgresSearchPattern.EscapeCharacter) ||
            EF.Functions.ILike(x.Customer.ExternalId, pattern, PostgresSearchPattern.EscapeCharacter) ||
            EF.Functions.ILike(x.Customer.CustomerName, pattern, PostgresSearchPattern.EscapeCharacter) ||
            (x.CreatedByNavigation != null && EF.Functions.ILike(
                x.CreatedByNavigation.FullName ?? string.Empty,
                pattern,
                PostgresSearchPattern.EscapeCharacter)) ||
            (x.Product.CreatedByNavigation != null && EF.Functions.ILike(
                x.Product.CreatedByNavigation.FullName ?? string.Empty,
                pattern,
                PostgresSearchPattern.EscapeCharacter)) ||
            (x.Formula != null && EF.Functions.ILike(
                x.Formula.ExternalId,
                pattern,
                PostgresSearchPattern.EscapeCharacter)) ||
            quotationLines.Any(line =>
                line.IsActive &&
                line.Quotation.IsActive &&
                line.Quotation.CompanyId == x.CompanyId &&
                line.Quotation.CustomerId == x.CustomerId &&
                (line.SampleRequestId == x.SampleRequestId ||
                 (line.SampleRequestId == null && line.ProductId == x.ProductId)) &&
                EF.Functions.ILike(
                    line.Quotation.ExternalId,
                    pattern,
                    PostgresSearchPattern.EscapeCharacter)));
    }

    private static bool IsIdentifierKeyword(string keyword)
        => keyword.Length >= 3 && keyword.All(character =>
            char.IsLetterOrDigit(character) || character is '_' or '-');

    private async Task<IQueryable<SampleRequest>> ApplyPricingViewAsync(
        IQueryable<SampleRequest> query,
        ProductPricingWorkbenchView view,
        Guid companyId,
        string currency,
        CancellationToken cancellationToken)
    {
        if (view == ProductPricingWorkbenchView.All)
        {
            // Unlike the product-centric Workbench, All must preserve the complete
            // Sample Request list because Sample Request is this endpoint's root.
            return query;
        }

        var currentVersions = _dbContext.ProductPricingVersions.AsNoTracking().Where(version =>
            version.CompanyId == companyId &&
            version.Currency == currency &&
            version.IsActive);

        return view switch
        {
            ProductPricingWorkbenchView.Draft => query.Where(sampleRequest =>
                currentVersions.Any(version =>
                    version.ProductId == sampleRequest.ProductId &&
                    version.Status == ProductPricingStatus.Draft)),

            ProductPricingWorkbenchView.Approved => query.Where(sampleRequest =>
                currentVersions.Any(version =>
                    version.ProductId == sampleRequest.ProductId &&
                    version.Status == ProductPricingStatus.Approved)),

            ProductPricingWorkbenchView.NeedsPricing =>
                ApplyNeedsPricingFilter(query, currentVersions, companyId)
                    .Union(await ApplyMaterialCostViewAsync(
                        query,
                        ProductPricingWorkbenchView.MaterialCostChanged,
                        companyId,
                        currency,
                        _featureOptions.MaterialCostChangeThresholdPercent,
                        cancellationToken)),

            ProductPricingWorkbenchView.MaterialCostChanged or
                ProductPricingWorkbenchView.ProductionMaterialCostChanged => await ApplyMaterialCostViewAsync(
                query,
                view,
                companyId,
                currency,
                view == ProductPricingWorkbenchView.MaterialCostChanged
                    ? 0m
                    : _featureOptions.MaterialCostChangeThresholdPercent,
                cancellationToken),

            _ => query.Where(_ => false)
        };
    }

    private async Task<IQueryable<SampleRequest>> ApplyMaterialCostViewAsync(
        IQueryable<SampleRequest> query,
        ProductPricingWorkbenchView view,
        Guid companyId,
        string currency,
        decimal thresholdPercent,
        CancellationToken cancellationToken)
    {
        // Keep the candidate product ids as a SQL subquery so only Approved baselines
        // with a positive snapshot are materialized before the batch realtime-cost resolver.
        var productIds = query.Select(x => x.ProductId).Distinct();

        var approvedVersions = await _dbContext.ProductPricingVersions
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == companyId &&
                x.IsActive &&
                x.Currency == currency &&
                x.Status == ProductPricingStatus.Approved &&
                productIds.Contains(x.ProductId) &&
                x.MaterialCostSnapshot > 0m)
            .Select(x => new PricingVersionRow
            {
                ProductId = x.ProductId,
                SourceType = x.SourceManufacturingFormulaId.HasValue
                    ? ProductPricingSourceType.ManufacturingFormula
                    : x.SourceFormulaId.HasValue
                        ? ProductPricingSourceType.Formula
                        : null,
                SourceId = x.SourceManufacturingFormulaId ?? x.SourceFormulaId,
                MaterialCostSnapshot = x.MaterialCostSnapshot,
                Version = x.Version,
                CreatedDate = x.CreatedDate,
                UpdatedDate = x.UpdatedDate
            })
            .ToListAsync(cancellationToken);
        var baselines = approvedVersions
            .GroupBy(x => x.ProductId)
            .Select(group => group
                .OrderByDescending(x => x.Version)
                .ThenByDescending(x => x.UpdatedDate ?? x.CreatedDate)
                .First())
            .Select(x => new ProductPricingMaterialCostBaseline(
                x.ProductId,
                x.MaterialCostSnapshot!.Value,
                x.SourceType.HasValue && x.SourceId.HasValue
                    ? new ProductPricingSourceSelection(
                        x.ProductId,
                        x.SourceType.Value,
                        x.SourceId.Value)
                    : null))
            .ToArray();
        var changedProductIds = await _sourceQueryService.LoadMaterialCostChangedProductIdsAsync(
            baselines,
            companyId,
            currency,
            thresholdPercent,
            view == ProductPricingWorkbenchView.ProductionMaterialCostChanged,
            cancellationToken);
        return changedProductIds.Count == 0
            ? query.Where(_ => false)
            : query.Where(x => changedProductIds.Contains(x.ProductId));
    }

    private IQueryable<SampleRequest> ApplyNeedsPricingFilter(
        IQueryable<SampleRequest> query,
        IQueryable<HRM.Domain.Entities.CustomerSchema.ProductPricingVersion> currentVersions,
        Guid companyId)
    {
        var requestedQuotations = _dbContext.InternalConversations
            .AsNoTracking()
            .Where(conversation =>
                conversation.CompanyId == companyId &&
                conversation.IsActive &&
                conversation.RelatedType == InternalMailRelatedType.Quotation &&
                conversation.RelatedId.HasValue &&
                conversation.Messages.Any(message =>
                    !message.IsDeleted &&
                    message.PayloadJson != null &&
                    EF.Functions.JsonContains(message.PayloadJson, QuotationRequestedPayload)))
            .Select(conversation => new
            {
                conversation.InternalConversationId,
                QuotationId = conversation.RelatedId!.Value,
                RequestedAt = conversation.Messages
                    .Where(message =>
                        !message.IsDeleted &&
                        message.PayloadJson != null &&
                        EF.Functions.JsonContains(message.PayloadJson, QuotationRequestedPayload))
                    .Max(message => message.SentAt)
            });
        var activeRequestedQuotations = requestedQuotations.Where(requested =>
            !_dbContext.InternalConversations.AsNoTracking().Any(conversation =>
                conversation.InternalConversationId == requested.InternalConversationId &&
                conversation.Messages.Any(message =>
                    !message.IsDeleted &&
                    message.PayloadJson != null &&
                    EF.Functions.JsonContains(message.PayloadJson, QuotationRequestWithdrawnPayload) &&
                    message.SentAt >= requested.RequestedAt)));

        var approvedVersions = currentVersions.Where(version =>
            version.Status == ProductPricingStatus.Approved);
        var latestApprovedVersions = approvedVersions.Where(version =>
            !approvedVersions.Any(candidate =>
                candidate.ProductId == version.ProductId &&
                (candidate.Version > version.Version ||
                 (candidate.Version == version.Version &&
                  (candidate.UpdatedDate ?? candidate.CreatedDate) >
                  (version.UpdatedDate ?? version.CreatedDate)))));

        var pendingSaleRequestProductIds =
            from line in _dbContext.QuotationLines.AsNoTracking()
            join requested in activeRequestedQuotations
                on line.QuotationId equals requested.QuotationId
            where line.IsActive &&
                  line.Quotation.IsActive &&
                  line.Quotation.CompanyId == companyId &&
                  !latestApprovedVersions.Any(version =>
                      version.ProductId == line.ProductId &&
                      (version.ApprovedAt ?? version.UpdatedDate ?? version.CreatedDate) >=
                      requested.RequestedAt)
            select line.ProductId;

        var reviewCutoff = _featureOptions.ApprovedPricingReviewAfterDays > 0
            ? _dateTimeProvider.Now.AddDays(-_featureOptions.ApprovedPricingReviewAfterDays)
            : (DateTime?)null;
        var reapprovalProductIds = latestApprovedVersions
            .Where(version =>
                (reviewCutoff.HasValue &&
                 (version.ApprovedAt ?? version.UpdatedDate ?? version.CreatedDate) <= reviewCutoff.Value) ||
                _dbContext.Formulas.AsNoTracking().Any(formula =>
                    formula.CompanyId == companyId &&
                    formula.IsActive &&
                    formula.ProductId == version.ProductId &&
                    formula.CheckDate.HasValue &&
                    formula.Status != FormulaStatus.Cancelled.ToString() &&
                    formula.Status != FormulaStatus.Rejected.ToString() &&
                    formula.CheckDate.Value >
                    (version.ApprovedAt ?? version.UpdatedDate ?? version.CreatedDate)))
            .Select(version => version.ProductId);

        return query.Where(sampleRequest =>
            reapprovalProductIds.Contains(sampleRequest.ProductId) ||
            pendingSaleRequestProductIds.Contains(sampleRequest.ProductId));
    }

    private static IQueryable<SampleRequestOverviewRow> ApplySorting(
        IQueryable<SampleRequestOverviewRow> query,
        GetSampleRequestPricingOverviewQuery request)
    {
        var descending = request.SortDescending;
        return request.NormalizedSortBy.ToLowerInvariant() switch
        {
            "createddate" => descending
                ? query.OrderByDescending(x => x.CreatedDate).ThenByDescending(x => x.SampleRequestId)
                : query.OrderBy(x => x.CreatedDate).ThenBy(x => x.SampleRequestId),
            "updateddate" => descending
                ? query.OrderByDescending(x => x.UpdatedDate).ThenByDescending(x => x.CreatedDate)
                : query.OrderBy(x => x.UpdatedDate).ThenByDescending(x => x.CreatedDate),
            "requestcode" or "externalid" => descending
                ? query.OrderByDescending(x => x.RequestCode).ThenByDescending(x => x.CreatedDate)
                : query.OrderBy(x => x.RequestCode).ThenByDescending(x => x.CreatedDate),
            "productcode" => descending
                ? query.OrderByDescending(x => x.ProductCode).ThenByDescending(x => x.CreatedDate)
                : query.OrderBy(x => x.ProductCode).ThenByDescending(x => x.CreatedDate),
            "productname" => descending
                ? query.OrderByDescending(x => x.ProductName).ThenByDescending(x => x.CreatedDate)
                : query.OrderBy(x => x.ProductName).ThenByDescending(x => x.CreatedDate),
            "colourcode" => descending
                ? query.OrderByDescending(x => x.ColourCode).ThenByDescending(x => x.CreatedDate)
                : query.OrderBy(x => x.ColourCode).ThenByDescending(x => x.CreatedDate),
            "customername" => descending
                ? query.OrderByDescending(x => x.CustomerName).ThenByDescending(x => x.CreatedDate)
                : query.OrderBy(x => x.CustomerName).ThenByDescending(x => x.CreatedDate),
            _ => descending
                ? query.OrderByDescending(x => x.LatestActivityAt)
                    .ThenByDescending(x => x.CreatedDate)
                : query.OrderBy(x => x.LatestActivityAt)
                    .ThenBy(x => x.CreatedDate)
        };
    }

    private async Task<PricingLoadResult> LoadPricingAsync(
        IReadOnlyCollection<Guid> productIds,
        Guid companyId,
        string currency,
        PricingAccessDecision pricingAccess,
        CancellationToken cancellationToken)
    {
        var versions = await _dbContext.ProductPricingVersions
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == companyId &&
                productIds.Contains(x.ProductId) &&
                x.Currency == currency &&
                x.IsActive &&
                (x.Status == ProductPricingStatus.Draft || x.Status == ProductPricingStatus.Approved))
            .Select(x => new PricingVersionRow
            {
                ProductPricingVersionId = x.ProductPricingVersionId,
                ProductId = x.ProductId,
                SourceType = x.SourceManufacturingFormulaId.HasValue
                    ? ProductPricingSourceType.ManufacturingFormula
                    : x.SourceFormulaId.HasValue ? ProductPricingSourceType.Formula : null,
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

        var currentByProduct = productIds.ToDictionary(
            productId => productId,
            productId => ResolveCurrentVersions(versions.Where(x => x.ProductId == productId)));
        var selections = currentByProduct.Values
            .Select(x => x.Preferred)
            .Where(x => x?.SourceType is not null && x.SourceId.HasValue)
            .Select(x => new ProductPricingSourceSelection(x!.ProductId, x.SourceType!.Value, x.SourceId!.Value))
            .Concat(currentByProduct.Values
                .Select(x => x.Approved)
                .Where(x => x?.SourceType is not null && x.SourceId.HasValue)
                .Select(x => new ProductPricingSourceSelection(
                    x!.ProductId,
                    x.SourceType!.Value,
                    x.SourceId!.Value)))
            .Distinct()
            .ToArray();
        // Executive pricing can use a VA selected from ManufacturingFormula/ProductionSelectVersion.
        // The legacy quotation resolver deliberately excludes that source, which would otherwise
        // turn an approved VA price into a misleading "no standard price" health state.
        var selectedSources = await _sourceQueryService.LoadSelectedForExecutiveAsync(
            selections, companyId, currency, cancellationToken);
        var fallbackProductIds = currentByProduct
            .Where(x => x.Value.Preferred is null)
            .Select(x => x.Key)
            .ToArray();
        var fallbackSources = await _sourceQueryService.LoadFallbackSelectedAsync(
            fallbackProductIds, companyId, currency, cancellationToken);
        var requests = await _requestQueryService.LoadAsync(companyId, productIds, cancellationToken);
        var requestsByProduct = requests.GroupBy(x => x.ProductId)
            .ToDictionary(x => x.Key, x => (IReadOnlyList<ProductPricingRequestRow>)x.ToArray());
        var latestFormulaConfirmations = await _dbContext.Formulas.AsNoTracking()
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
        var latestFormulaConfirmationByProduct = latestFormulaConfirmations
            .Where(x => x.ConfirmedAt.HasValue)
            .ToDictionary(x => x.ProductId, x => x.ConfirmedAt!.Value);
        var productRows = await _dbContext.SampleRequests
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.IsActive && productIds.Contains(x.ProductId))
            .Select(x => new ProductRow
            {
                ProductId = x.ProductId,
                ProductCode = x.Product.ColourCode ?? x.Product.Code ?? string.Empty,
                ProductName = x.Product.Name ?? string.Empty
            })
            .Distinct()
            .ToListAsync(cancellationToken);
        var now = _dateTimeProvider.Now;
        var comparisonRequests = currentByProduct.Values
            .Where(x => x.Approved is not null)
            .Select(x =>
            {
                var approved = x.Approved!;
                return new StandardPriceRealtimeComparisonRequest(
                    approved.ProductId,
                    currency,
                    approved.StandardSellingPrice,
                    approved.MaterialCostSnapshot,
                    approved.SourceType,
                    approved.SourceId);
            })
            .ToArray();
        var comparisonsByProduct = _comparisonQueryService.BuildVisible(
            comparisonRequests,
            selectedSources,
            pricingAccess);

        ProductPricingSourceOptionDto? ResolveSource(Guid productId)
        {
            var current = currentByProduct[productId];
            if (current.Preferred?.SourceType is { } sourceType && current.Preferred.SourceId is { } sourceId)
            {
                return selectedSources.GetValueOrDefault(
                    new ProductPricingSourceSelection(productId, sourceType, sourceId));
            }

            return fallbackSources.GetValueOrDefault(productId);
        }

        var sourceByProduct = productRows.ToDictionary(
            product => product.ProductId,
            product => ResolveSource(product.ProductId));
        var byProduct = productRows.ToDictionary(product => product.ProductId, product =>
        {
            var current = currentByProduct[product.ProductId];
            var source = sourceByProduct[product.ProductId];

            var productRequests = requestsByProduct.GetValueOrDefault(product.ProductId) ?? [];
            var health = ProductPricingHealthEvaluator.Evaluate(
                source,
                current.Draft,
                current.Approved,
                now,
                _featureOptions,
                latestFormulaConfirmationByProduct.GetValueOrDefault(product.ProductId));
            return ProductPricingWorkbenchVisibility.ApplyToSummary(
                ProductPricingWorkbenchMapper.MapSummary(
                    product,
                    currency,
                    current.Draft,
                    current.Approved,
                    source,
                    productRequests,
                    health: health,
                    now: now,
                    realtimePriceComparison: comparisonsByProduct.GetValueOrDefault(product.ProductId)),
                pricingAccess);
        });

        var materialsByProduct = sourceByProduct.ToDictionary(
            pair => pair.Key,
            pair => pricingAccess.CanViewMaterialCost
                ? pair.Value?.Materials ?? []
                : (IReadOnlyList<QuotationProductPricingMaterialDto>)[]);
        return new PricingLoadResult(byProduct, materialsByProduct);
    }

    private async Task<IReadOnlyDictionary<Guid, ConversationOverviewRow>> LoadConversationsAsync(
        IReadOnlyCollection<Guid> sampleRequestIds,
        Guid companyId,
        Guid employeeId,
        bool canReadExecutiveSampleRequestConversations,
        CancellationToken cancellationToken)
    {
        var rows = await _dbContext.InternalConversations
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == companyId &&
                x.IsActive &&
                x.RelatedType == InternalMailRelatedType.SampleRequest &&
                x.RelatedId.HasValue &&
                sampleRequestIds.Contains(x.RelatedId.Value))
            .Select(x => new ConversationOverviewRow
            {
                SampleRequestId = x.RelatedId!.Value,
                ConversationId = x.InternalConversationId,
                TotalMessageCount = x.Messages.Count(message => !message.IsDeleted),
                UnreadCount = x.Messages.Count(message =>
                    !message.IsDeleted &&
                    message.SenderEmployeeId != employeeId &&
                    message.ReadStates.Any(state => state.EmployeeId == employeeId && !state.IsRead)),
                LastMessageAt = x.LastMessageAt,
                CanOpen = canReadExecutiveSampleRequestConversations ||
                    x.Participants.Any(participant =>
                        participant.EmployeeId == employeeId && participant.IsActive)
            })
            .ToListAsync(cancellationToken);

        return rows.GroupBy(x => x.SampleRequestId)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.LastMessageAt).First());
    }

    internal static CurrentPricingRows ResolveCurrentVersions(IEnumerable<PricingVersionRow> versions)
    {
        PricingVersionRow? Latest(ProductPricingStatus status) => versions
            .Where(x => x.Status == status)
            .OrderByDescending(x => x.Version)
            .ThenByDescending(x => x.UpdatedDate ?? x.CreatedDate)
            .FirstOrDefault();

        return new CurrentPricingRows(
            Latest(ProductPricingStatus.Draft),
            Latest(ProductPricingStatus.Approved));
    }

    internal static SampleRequestPricingOverviewItemDto MapItem(
        SampleRequestOverviewRow row,
        ProductPricingWorkbenchItemDto? pricing,
        ConversationOverviewRow? conversation,
        IReadOnlyList<QuotationProductPricingMaterialDto>? materials = null)
        => new()
        {
            SampleRequestId = row.SampleRequestId,
            RequestCode = row.RequestCode,
            CreatedDate = row.CreatedDate,
            Status = row.Status,
            LatestActivityAt = new[]
            {
                row.LatestActivityAt,
                conversation?.LastMessageAt ?? DateTime.MinValue,
                pricing?.PricingUpdatedDate ?? DateTime.MinValue
            }.Max(),
            Product = new SampleRequestPricingProductDto
            {
                ProductId = row.ProductId,
                Code = row.ProductCode,
                Name = row.ProductName,
                ColourCode = row.ColourCode,
                ColorValue = row.ColourName,
                ColorDisplayName = row.ColourName,
                CategoryId = row.CategoryId,
                CategoryCode = row.CategoryCode,
                CategoryName = row.CategoryName,
                AdditiveCode = row.AdditiveCode,
                AdditiveDisplayName = row.AdditiveCode,
                LabEmployeeId = row.LabEmployeeId,
                LabName = row.LabName
            },
            Customer = new SampleRequestPricingCustomerDto
            {
                CustomerId = row.CustomerId,
                Code = row.CustomerCode,
                Name = row.CustomerName,
                SaleEmployeeId = row.SaleEmployeeId,
                SaleName = row.SaleName
            },
            Delivery = new SampleRequestPricingDeliveryDto
            {
                RequestedDate = row.RequestedDeliveryDate,
                ExpectedDate = row.ExpectedDeliveryDate
            },
            Pricing = MapPricing(pricing, materials),
            Conversation = conversation is null
                ? new SampleRequestConversationOverviewDto()
                : new SampleRequestConversationOverviewDto
                {
                    ConversationId = conversation.ConversationId,
                    TotalMessageCount = conversation.TotalMessageCount,
                    UnreadCount = conversation.UnreadCount,
                    LastMessageAt = conversation.LastMessageAt
                },
            Actions = new SampleRequestPricingActionsDto
            {
                CanOpenSampleRequest = true,
                CanOpenPricingDetail = pricing?.CanOpenPricingDetail == true,
                CanManagePricing = pricing?.CanManagePricing == true,
                CanOpenConversation = conversation?.CanOpen == true
            }
        };

    internal static SampleRequestPricingDto MapPricing(
        ProductPricingWorkbenchItemDto? pricing,
        IReadOnlyList<QuotationProductPricingMaterialDto>? materials = null)
    {
        if (pricing is null)
        {
            return new SampleRequestPricingDto
            {
                Currency = "VND",
                PricingStatus = ProductPricingLookupStatus.NoEligibleSource,
                PricingHealthStatus = ProductPricingHealthStatus.NoEligibleSource,
                RequiresPricingAction = true,
                StandardPriceState = ProductStandardPriceState.Missing,
                Materials = materials ?? []
            };
        }

        var costBase = pricing.CurrentMaterialCost.HasValue && pricing.ManufacturingCost.HasValue
            ? pricing.CurrentMaterialCost + pricing.ManufacturingCost
            : null;
        return new SampleRequestPricingDto
        {
            Currency = pricing.Currency,
            StandardSellingPrice = pricing.StandardSellingPrice,
            PublisherNote = pricing.PublisherNote,
            CurrentMaterialCost = pricing.CurrentMaterialCost,
            ManufacturingCost = pricing.ManufacturingCost,
            ProfitMarginRate = pricing.ProfitMarginRate,
            ProfitMarginPercent = PricingMarginCalculator.CalculateProfitMarginPercent(
                pricing.StandardSellingPrice,
                costBase),
            StandardSellingPriceDifference = pricing.StandardSellingPriceDifference,
            StandardSellingPriceDifferencePercent = pricing.StandardSellingPriceDifferencePercent,
            HasRealtimePriceComparison = pricing.HasRealtimePriceComparison,
            RealtimePriceComparison = pricing.RealtimePriceComparison,
            Materials = materials ?? [],
            PricingStatus = pricing.PricingStatus,
            PricingHealthStatus = pricing.PricingHealthStatus,
            RequiresPricingAction = pricing.RequiresPricingAction,
            PricingReviewDueDate = pricing.PricingReviewDueDate,
            StandardPriceState = pricing.StandardPriceState,
            HasFormulaConfirmationPending = pricing.HasFormulaConfirmationPending,
            IsPricingReviewExpired = pricing.IsPricingReviewExpired,
            PricingAttentionSources = pricing.PricingAttentionSources,
            PricingUpdatedDate = pricing.PricingUpdatedDate,
            WaitingQuotationCount = pricing.WaitingQuotationCount,
            DraftPricingVersionId = pricing.DraftPricingVersionId,
            ApprovedPricingVersionId = pricing.ApprovedPricingVersionId,
            DisplayedFormula = pricing.SourceType.HasValue && pricing.SourceId.HasValue
                ? new SampleRequestDisplayedPricingFormulaDto
                {
                    SourceType = pricing.SourceType.Value,
                    SourceId = pricing.SourceId.Value,
                    Code = pricing.SourceExternalId ?? string.Empty,
                    Name = pricing.SourceName ?? string.Empty,
                    Status = pricing.SourceStatus,
                    PriceKind = pricing.IsSystemCalculatedDraft
                        ? "SystemCalculated"
                        : "Standard"
                }
                : null
        };
    }

    private string? Validate(GetSampleRequestPricingOverviewQuery request)
    {
        if (!CanAccess(_currentUser))
            return "Only President or Developer can access the executive pricing overview.";
        if (!_currentUser.CompanyId.HasValue || _currentUser.CompanyId.Value == Guid.Empty)
            return "Current company context is required.";
        if (!_currentUser.EmployeeId.HasValue || _currentUser.EmployeeId.Value == Guid.Empty)
            return "Current employee context is required.";
        if (!string.Equals(request.NormalizedCurrency, "VND", StringComparison.Ordinal))
            return "Product standard pricing is managed in VND only.";
        if (!Enum.IsDefined(request.View))
            return "view is invalid.";
        if (request.SearchType.HasValue && !Enum.IsDefined(request.SearchType.Value))
            return "searchType is invalid.";
        if (request.FromDate.HasValue && request.ToDate.HasValue &&
            request.FromDate.Value.Date > request.ToDate.Value.Date)
            return "fromDate cannot be later than toDate.";
        if (!SupportedSortFields.Contains(request.NormalizedSortBy, StringComparer.OrdinalIgnoreCase))
            return $"sortBy must be one of: {string.Join(", ", SupportedSortFields)}.";
        return null;
    }

    internal static bool CanAccess(ICurrentUser currentUser)
        => currentUser.IsInRole(ApplicationRoles.President) ||
           currentUser.IsInRole(ApplicationRoles.Developer);

    private sealed record PricingLoadResult(
        IReadOnlyDictionary<Guid, ProductPricingWorkbenchItemDto> ByProduct,
        IReadOnlyDictionary<Guid, IReadOnlyList<QuotationProductPricingMaterialDto>> MaterialsByProduct);

    private static OperationResult<PagedResult<SampleRequestPricingOverviewItemDto>> Ok(
        IReadOnlyList<SampleRequestPricingOverviewItemDto> items,
        int totalCount,
        GetSampleRequestPricingOverviewQuery request)
        => OperationResult<PagedResult<SampleRequestPricingOverviewItemDto>>.Ok(
            new PagedResult<SampleRequestPricingOverviewItemDto>(
                items,
                totalCount,
                request.NormalizedPageNumber,
                request.NormalizedPageSize));
}
