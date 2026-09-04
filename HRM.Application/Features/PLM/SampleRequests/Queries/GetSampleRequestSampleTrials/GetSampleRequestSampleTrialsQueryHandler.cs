using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Authorization.PLM;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Application.Features.PLM.Shared.Rules;
using HRM.Application.Features.PLM.SampleRequests.Dtos.SampleTrials;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials.Models;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Domain.Enums.SampleRequests;
using HRM.Domain.Enums.CustomerEnum;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials;

internal sealed class GetSampleRequestSampleTrialsQueryHandler
    : IRequestHandler<GetSampleRequestSampleTrialsQuery, PagedResult<SampleRequestSampleTrialReportDto>>
{
    private readonly IPLMReadDbContext _dbContext;
    private readonly ICRMReadDbContext _crmDbContext;
    private readonly ICustomerVisibilityService _visibilityService;
    private readonly IPLMFieldVisibilityService _fieldVisibility;
    private readonly ICurrentUser _currentUser;
    private readonly ProductPricingSourceQueryService _pricingSourceQueryService;

    public GetSampleRequestSampleTrialsQueryHandler(
        IPLMReadDbContext dbContext,
        ICRMReadDbContext crmDbContext,
        ICustomerVisibilityService visibilityService,
        IPLMFieldVisibilityService fieldVisibility,
        ICurrentUser currentUser,
        ProductPricingSourceQueryService pricingSourceQueryService)
    {
        _dbContext = dbContext;
        _crmDbContext = crmDbContext;
        _visibilityService = visibilityService;
        _fieldVisibility = fieldVisibility;
        _currentUser = currentUser;
        _pricingSourceQueryService = pricingSourceQueryService;
    }

    public async Task<PagedResult<SampleRequestSampleTrialReportDto>> Handle(
        GetSampleRequestSampleTrialsQuery request,
        CancellationToken cancellationToken)
    {
        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
        var visibleSampleRequests = _visibilityService.ApplySampleRequestVisibility(
            _dbContext.SampleRequests.AsNoTracking().Where(x => x.IsActive),
            _dbContext.Customers.AsNoTracking(),
            scope)
            .Where(x => x.Customer.ExternalId != PLMCustomerRules.InternalCustomerExternalId);

        if (request.SampleRequestId.HasValue)
        {
            visibleSampleRequests = visibleSampleRequests.Where(
                x => x.SampleRequestId == request.SampleRequestId.Value);
        }

        if (request.CustomerId.HasValue)
        {
            visibleSampleRequests = visibleSampleRequests.Where(
                x => x.CustomerId == request.CustomerId.Value);
        }

        var sampleRequestCreatedRange =
            SampleRequestSampleTrialReportRules.ResolveCreatedRange(
                request.SampleRequestCreatedToDate,
                request.IncludePreviousUnfinished);
        if (sampleRequestCreatedRange.FromInclusive.HasValue)
        {
            visibleSampleRequests = visibleSampleRequests.Where(
                x => x.CreatedDate >= sampleRequestCreatedRange.FromInclusive.Value);
        }

        var sampleRequestCreatedToExclusive = sampleRequestCreatedRange.ToExclusive;
        if (sampleRequestCreatedToExclusive.HasValue)
        {
            visibleSampleRequests = visibleSampleRequests.Where(
                x => x.CreatedDate < sampleRequestCreatedToExclusive.Value);
        }

        var activeTrials = _dbContext.SampleRequestSampleTrials
            .AsNoTracking()
            .Where(x => x.IsActive);

        var trialCounts =
            from trial in activeTrials
            group trial by trial.SampleRequestId into trialGroup
            select new
            {
                SampleRequestId = trialGroup.Key,
                Count = trialGroup.Count(),
                LatestTrialNo = trialGroup.Max(x => x.TrialNo)
            };

        var latestTrials =
            from trial in activeTrials
            join trialCount in trialCounts
                on new { trial.SampleRequestId, trial.TrialNo }
                equals new { trialCount.SampleRequestId, TrialNo = trialCount.LatestTrialNo }
            select trial;

        var reportTrials = request.IncludeTrialHistory ? activeTrials : latestTrials;

        var query =
            from sampleRequest in visibleSampleRequests
            join trialCount in trialCounts
                on sampleRequest.SampleRequestId equals trialCount.SampleRequestId into trialCountGroup
            from trialCount in trialCountGroup.DefaultIfEmpty()
            join trial in reportTrials
                on sampleRequest.SampleRequestId equals trial.SampleRequestId into trialGroup
            from trial in trialGroup.DefaultIfEmpty()
            select new SampleRequestSampleTrialReportRow
            {
                SampleRequest = sampleRequest,
                Trial = trial,
                TrialCount = trialCount == null ? null : trialCount.Count
            };

        query = SampleRequestSampleTrialReportQueryRules.ApplyReportType(query, request);
        query = SampleRequestSampleTrialReportQueryRules.ApplyDateRange(query, request);

        if (request.Status.HasValue)
        {
            query = query.Where(x => x.Trial != null && x.Trial.Status == request.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.CustomerReplyStatus))
        {
            var replyStatus = request.CustomerReplyStatus.Trim();
            query = query.Where(x => x.Trial != null && x.Trial.CustomerReplyStatus == replyStatus);
        }

        if (!string.IsNullOrWhiteSpace(request.NormalizedKeyword))
        {
            var keyword = request.NormalizedKeyword;
            query = query.Where(x =>
                ((x.Trial != null ? x.Trial.CustomerNameSnapshot : null) ?? x.SampleRequest.Customer.CustomerName).Contains(keyword) ||
                ((x.Trial != null ? x.Trial.SampleRequestExternalIdSnapshot : null) ?? x.SampleRequest.ExternalId).Contains(keyword) ||
                ((x.Trial != null ? x.Trial.ProductNameSnapshot : null) ?? x.SampleRequest.Product.Name ?? string.Empty).Contains(keyword) ||
                ((x.Trial != null ? x.Trial.ColourCodeSnapshot : null) ?? x.SampleRequest.Product.ColourCode ?? string.Empty).Contains(keyword) ||
                (x.SampleRequest.Formula != null && EF.Functions.ILike(x.SampleRequest.Formula.ExternalId, $"%{keyword}%")) ||
                (x.Trial != null && x.Trial.Formula != null && EF.Functions.ILike(x.Trial.Formula.ExternalId, $"%{keyword}%")) ||
                (x.Trial != null && (x.Trial.BatchNo ?? string.Empty).Contains(keyword)) ||
                (x.Trial != null && (x.Trial.CustomerReplyNote ?? string.Empty).Contains(keyword)) ||
                (x.Trial != null && (x.Trial.LabNote ?? string.Empty).Contains(keyword)));
        }

        query = SampleRequestSampleTrialReportQueryRules.ApplySorting(query, request);
        var totalCount = await query.CountAsync(cancellationToken);
        var canViewTechnicalFields = _fieldVisibility.CanViewProductTechnicalInfo();
        var canUpdateCustomerFeedback =
            canViewTechnicalFields ||
            _currentUser.IsInAnyRole(ApplicationRoleSets.Modules.Sales);

        var items = await query
            .Skip((request.NormalizedPageNumber - 1) * request.NormalizedPageSize)
            .Take(request.NormalizedPageSize)
            .Select(x => new SampleRequestSampleTrialReportDto
            {
                SampleRequestSampleTrialId = x.Trial != null ? x.Trial.SampleRequestSampleTrialId : null,
                SampleRequestId = x.SampleRequest.SampleRequestId,
                FormulaId = x.Trial != null ? x.Trial.FormulaId : x.SampleRequest.FormulaId,
                FormulaExternalId = x.Trial != null
                    ? x.Trial.Formula != null
                        ? x.Trial.Formula.ExternalId
                        : x.Trial.BatchNo
                    : x.SampleRequest.Formula != null
                        ? x.SampleRequest.Formula.ExternalId
                        : null,
                TrialNo = x.Trial != null ? x.Trial.TrialNo : null,
                HasTrial = x.Trial != null,
                TrialCount = x.TrialCount ?? 0,
                HasPreviousTrials = (x.TrialCount ?? 0) > 1,
                CanCreateTrial = canViewTechnicalFields,
                CanUpdateTrial = canViewTechnicalFields && x.Trial != null,
                CanUpdateCustomerFeedback = canUpdateCustomerFeedback &&
                    x.Trial != null &&
                    (x.Trial.Status == SampleTrialStatus.SampleSent ||
                     x.Trial.Status == SampleTrialStatus.WaitingCustomerFeedback ||
                     x.Trial.Status == SampleTrialStatus.PriceQuote),

                SampleRequestStatus = x.SampleRequest.Status,
                CustomerName = x.SampleRequest.Customer.CustomerName,
                ManagerSalesName = x.SampleRequest.ManagerByNavigation.FullName,

                SampleRequestExternalId = x.SampleRequest.ExternalId,
                RequestedSampleQuantity = x.SampleRequest.SampleQuantity,
                DeliveredSampleQuantityKg = x.Trial != null ? x.Trial.DeliveredSampleQuantityKg : null,

                ProductName = x.SampleRequest.Product.Name ?? string.Empty,
                CategoryName = x.SampleRequest.Product.Category != null ? x.SampleRequest.Product.Category.Name : null,
                ColourCode = x.SampleRequest.Product.ColourCode,
                BatchNo = x.Trial != null ? x.Trial.BatchNo : null,
                RequestDeliveryDate = x.SampleRequest.RequestDeliveryDate,
                ExpectedDeliveryDate = x.SampleRequest.ExpectedDeliveryDate,
                RequestReceivedDate = x.Trial != null ? x.Trial.RequestReceivedDate : null,
                FinishedDate = x.Trial != null ? x.Trial.FinishedDate : null,

                SentDate = x.Trial != null ? x.Trial.SentDate : null,
                SentByEmployeeId = x.Trial != null ? x.Trial.SentByEmployeeId : x.SampleRequest.SendBy,
                SentByName = x.Trial != null && x.Trial.SentByEmployeeId.HasValue
                    ? _dbContext.Employees
                        .Where(employee => employee.EmployeeId == x.Trial.SentByEmployeeId.Value)
                        .Select(employee => employee.FullName)
                        .FirstOrDefault()
                    : x.SampleRequest.SendByNavigation != null
                        ? x.SampleRequest.SendByNavigation.FullName
                        : null,

                DeliveryMethod = x.Trial != null ? x.Trial.DeliveryMethod : null,
                Status = x.Trial != null ? x.Trial.Status : null,
                CustomerReplyStatus = x.Trial != null ? x.Trial.CustomerReplyStatus : null,
                CustomerReplyDate = x.Trial != null ? x.Trial.CustomerReplyDate : null,
                CustomerReplyNote = x.Trial != null ? x.Trial.CustomerReplyNote : null,
                OrderDate = x.Trial != null ? x.Trial.OrderDate : null,
                AdditiveRate = x.SampleRequest.Product.UsageRate != null ? x.SampleRequest.Product.UsageRate : null ,
                LabNote = canViewTechnicalFields && x.Trial != null ? x.Trial.LabNote : null,
                SampleRequestCreatedDate = x.SampleRequest.CreatedDate,
                CreatedDate = x.Trial != null ? x.Trial.CreatedDate : x.SampleRequest.CreatedDate,
                UpdatedDate = x.Trial != null ? x.Trial.UpdatedDate : x.SampleRequest.UpdatedDate
            })
            .ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            item.TurnaroundDays = SampleRequestSampleTrialReportRules.CalculateTurnaroundDays(
                item.RequestReceivedDate,
                item.FinishedDate);
        }

        await AttachStandardSellingPricesAsync(
            items,
            scope.CompanyId,
            request.NormalizedCurrency,
            cancellationToken);

        return new PagedResult<SampleRequestSampleTrialReportDto>(
            items,
            totalCount,
            request.NormalizedPageNumber,
            request.NormalizedPageSize);
    }

    private async Task AttachStandardSellingPricesAsync(
        IReadOnlyCollection<SampleRequestSampleTrialReportDto> items,
        Guid companyId,
        string currency,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0 || !ProductPricingAccessRules.CanViewWorkbench(_currentUser))
        {
            return;
        }

        var sampleRequestIds = items
            .Select(x => x.SampleRequestId)
            .Distinct()
            .ToArray();
        var productRows = await _dbContext.SampleRequests
            .AsNoTracking()
            .Where(x =>
                sampleRequestIds.Contains(x.SampleRequestId) &&
                x.CompanyId == companyId &&
                x.IsActive)
            .Select(x => new
            {
                x.SampleRequestId,
                x.ProductId
            })
            .ToListAsync(cancellationToken);
        var productBySampleRequest = productRows.ToDictionary(
            x => x.SampleRequestId,
            x => x.ProductId);
        var productIds = productRows
            .Select(x => x.ProductId)
            .Distinct()
            .ToArray();
        if (productIds.Length == 0)
        {
            return;
        }

        var approvedPricingRows = await _crmDbContext.ProductPricingVersions
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == companyId &&
                productIds.Contains(x.ProductId) &&
                x.Currency == currency &&
                x.IsActive &&
                x.Status == ProductPricingStatus.Approved &&
                x.StandardSellingPrice > 0m)
            .OrderByDescending(x => x.Version)
            .Select(x => new
            {
                x.ProductId,
                x.StandardSellingPrice,
                x.ApprovedAt,
                x.Version
            })
            .ToListAsync(cancellationToken);
        var approvedPricingByProduct = approvedPricingRows
            .GroupBy(x => x.ProductId)
            .ToDictionary(x => x.Key, x => x.First());

        var pricingSourcesByProduct = await _pricingSourceQueryService.LoadAsync(
            productIds,
            companyId,
            currency,
            includeSensitivePricing: false,
            cancellationToken);

        foreach (var item in items)
        {
            if (!productBySampleRequest.TryGetValue(item.SampleRequestId, out var productId))
            {
                continue;
            }

            if (approvedPricingByProduct.TryGetValue(productId, out var approvedPricing))
            {
                item.ApprovedStandardSellingPrice = approvedPricing.StandardSellingPrice;
                item.StandardSellingPriceApprovedAt = approvedPricing.ApprovedAt;
            }

            var sources = pricingSourcesByProduct.GetValueOrDefault(productId) ?? [];
            var formulaSource = item.FormulaId.HasValue
                ? sources.FirstOrDefault(source =>
                    source.SourceType == ProductPricingSourceType.Formula &&
                    source.SourceId == item.FormulaId.Value &&
                    source.IsEligible)
                : null;
            var effectiveSource = formulaSource ?? sources.FirstOrDefault(source => source.IsEligible);
            item.SystemCalculatedStandardSellingPrice = effectiveSource?.StandardSellingPrice;
        }
    }

}
