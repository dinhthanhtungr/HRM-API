using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Authorization.PLM;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.Shared.Rules;
using HRM.Application.Features.PLM.SampleRequests.Dtos.SampleTrials;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials.Models;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Domain.Enums.SampleRequests;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials;

internal sealed class GetSampleRequestSampleTrialsQueryHandler
    : IRequestHandler<GetSampleRequestSampleTrialsQuery, PagedResult<SampleRequestSampleTrialReportDto>>
{
    private readonly IPLMReadDbContext _dbContext;
    private readonly ICustomerVisibilityService _visibilityService;
    private readonly IPLMFieldVisibilityService _fieldVisibility;
    private readonly ICurrentUser _currentUser;

    public GetSampleRequestSampleTrialsQueryHandler(
        IPLMReadDbContext dbContext,
        ICustomerVisibilityService visibilityService,
        IPLMFieldVisibilityService fieldVisibility,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _visibilityService = visibilityService;
        _fieldVisibility = fieldVisibility;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<SampleRequestSampleTrialReportDto>> Handle(
        GetSampleRequestSampleTrialsQuery request,
        CancellationToken cancellationToken)
    {
        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
        var visibleSampleRequests = _visibilityService.ApplySampleRequestVisibility(
            _dbContext.SampleRequests.AsNoTracking().Where(x => x.Status != SampleRequestStatus.Cancelled.ToString() && x.IsActive),
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

        var activeTrials = _dbContext.SampleRequestSampleTrials
            .AsNoTracking()
            .Where(x => x.IsActive );

        var query =
            from sampleRequest in visibleSampleRequests
            join trial in activeTrials
                on sampleRequest.SampleRequestId equals trial.SampleRequestId into trialGroup
            from trial in trialGroup.DefaultIfEmpty()
            select new SampleRequestSampleTrialReportRow
            {
                SampleRequest = sampleRequest,
                Trial = trial
            };

        if (request.ReportType == SampleTrialReportType.CompletedSamples)
        {
            query = query.Where(x => x.Trial != null && x.Trial.FinishedDate.HasValue);

            if (request.FromDate.HasValue)
            {
                var fromDate = request.FromDate.Value.Date;
                query = query.Where(x => x.Trial!.FinishedDate >= fromDate);
            }

            if (request.ToDate.HasValue)
            {
                var toDateExclusive = request.ToDate.Value.Date.AddDays(1);
                query = query.Where(x => x.Trial!.FinishedDate < toDateExclusive);
            }
        }
        else if (request.ReportType == SampleTrialReportType.WaitingCustomerFeedback)
        {
            query = query.Where(x =>
                x.Trial != null &&
                x.Trial.RequestReceivedDate.HasValue &&
                x.Trial.Status == SampleTrialStatus.WaitingCustomerFeedback &&
                (x.Trial.CustomerReplyStatus == null ||
                 x.Trial.CustomerReplyStatus == string.Empty ||
                 x.Trial.CustomerReplyStatus == "WAITING"));

            if (request.FromDate.HasValue)
            {
                var fromDate = request.FromDate.Value.Date;
                query = query.Where(x => x.Trial!.RequestReceivedDate >= fromDate);
            }

            if (request.ToDate.HasValue)
            {
                var toDateExclusive = request.ToDate.Value.Date.AddDays(1);
                query = query.Where(x => x.Trial!.RequestReceivedDate < toDateExclusive);
            }
        }
        else
        {
            if (request.FromDate.HasValue)
            {
                var fromDate = request.FromDate.Value.Date;
                query = query.Where(x =>
                    (x.Trial != null
                        ? x.Trial.FinishedDate ?? x.Trial.SentDate ?? x.Trial.RequestReceivedDate ?? x.Trial.CreatedDate
                        : x.SampleRequest.CreatedDate) >= fromDate);
            }

            if (request.ToDate.HasValue)
            {
                var toDateExclusive = request.ToDate.Value.Date.AddDays(1);
                query = query.Where(x =>
                    (x.Trial != null
                        ? x.Trial.FinishedDate ?? x.Trial.SentDate ?? x.Trial.RequestReceivedDate ?? x.Trial.CreatedDate
                        : x.SampleRequest.CreatedDate) < toDateExclusive);
            }
        }

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
                (x.Trial != null && (x.Trial.BatchNo ?? string.Empty).Contains(keyword)) ||
                (x.Trial != null && (x.Trial.CustomerReplyNote ?? string.Empty).Contains(keyword)) ||
                (x.Trial != null && (x.Trial.LabNote ?? string.Empty).Contains(keyword)));
        }

        query = ApplySorting(query, request);
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
                TrialNo = x.Trial != null ? x.Trial.TrialNo : null,
                HasTrial = x.Trial != null,
                CanCreateTrial = canViewTechnicalFields,
                CanUpdateTrial = canViewTechnicalFields && x.Trial != null,
                CanUpdateCustomerFeedback = canUpdateCustomerFeedback && x.Trial != null,

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

        return new PagedResult<SampleRequestSampleTrialReportDto>(
            items,
            totalCount,
            request.NormalizedPageNumber,
            request.NormalizedPageSize);
    }

    private static IQueryable<SampleRequestSampleTrialReportRow> ApplySorting(
        IQueryable<SampleRequestSampleTrialReportRow> query,
        GetSampleRequestSampleTrialsQuery request)
    {
        return request.NormalizedSortBy switch
        {
            SampleRequestSampleTrialSortFields.SampleRequestExternalId => request.SortDescending
                ? query.OrderByDescending(x => (x.Trial != null ? x.Trial.SampleRequestExternalIdSnapshot : null) ?? x.SampleRequest.ExternalId)
                : query.OrderBy(x => (x.Trial != null ? x.Trial.SampleRequestExternalIdSnapshot : null) ?? x.SampleRequest.ExternalId),
            SampleRequestSampleTrialSortFields.CustomerName => request.SortDescending
                ? query.OrderByDescending(x => (x.Trial != null ? x.Trial.CustomerNameSnapshot : null) ?? x.SampleRequest.Customer.CustomerName)
                : query.OrderBy(x => (x.Trial != null ? x.Trial.CustomerNameSnapshot : null) ?? x.SampleRequest.Customer.CustomerName),
            SampleRequestSampleTrialSortFields.TrialNo => request.SortDescending
                ? query.OrderByDescending(x => x.Trial != null ? x.Trial.TrialNo : (int?)null)
                : query.OrderBy(x => x.Trial != null ? x.Trial.TrialNo : (int?)null),
            SampleRequestSampleTrialSortFields.RequestReceivedDate => request.SortDescending
                ? query.OrderByDescending(x => x.Trial != null ? x.Trial.RequestReceivedDate : null)
                : query.OrderBy(x => x.Trial != null ? x.Trial.RequestReceivedDate : null),
            SampleRequestSampleTrialSortFields.FinishedDate => request.SortDescending
                ? query.OrderByDescending(x => x.Trial != null ? x.Trial.FinishedDate : null)
                : query.OrderBy(x => x.Trial != null ? x.Trial.FinishedDate : null),
            SampleRequestSampleTrialSortFields.SentDate => request.SortDescending
                ? query.OrderByDescending(x => x.Trial != null ? x.Trial.SentDate : x.SampleRequest.SendDate)
                : query.OrderBy(x => x.Trial != null ? x.Trial.SentDate : x.SampleRequest.SendDate),
            SampleRequestSampleTrialSortFields.UpdatedDate => request.SortDescending
                ? query.OrderByDescending(x => x.Trial != null ? x.Trial.UpdatedDate : x.SampleRequest.UpdatedDate)
                : query.OrderBy(x => x.Trial != null ? x.Trial.UpdatedDate : x.SampleRequest.UpdatedDate),
            _ => query
                .OrderByDescending(x => x.SampleRequest.CreatedDate)
                .ThenByDescending(x => x.Trial != null ? x.Trial.TrialNo : (int?)null)
        };
    }

}
