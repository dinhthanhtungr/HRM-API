using HRM.Application.Commons.Authorization;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.PLM.Shared.Authorization;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.SampleRequests;
using HRM.Application.Features.PLM.SampleRequests.Dtos.Summary;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSummary.Models;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSummary.Services;
using HRM.Domain.Entities.SampleRequestSchema;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSummary;

internal sealed class GetSampleRequestSummaryQueryHandler
    : IRequestHandler<GetSampleRequestSummaryQuery, PagedResult<SampleRequestSummaryDto>>
{
    private readonly IPLMReadDbContext _dbContext;
    private readonly ICustomerVisibilityService _visibilityService;
    private readonly IPLMFieldVisibilityService _fieldVisibility;
    private readonly ICurrentUser? _user;
    private readonly ICurrentUserPermissionService? _permissions;

    public GetSampleRequestSummaryQueryHandler(
        IPLMReadDbContext dbContext,
        ICustomerVisibilityService visibilityService,
        IPLMFieldVisibilityService fieldVisibility,
        ICurrentUser? user = null, ICurrentUserPermissionService? permissions = null)
    {
        _dbContext = dbContext;
        _visibilityService = visibilityService;
        _fieldVisibility = fieldVisibility;
        _user = user;
        _permissions = permissions;
    }

    public async Task<PagedResult<SampleRequestSummaryDto>> Handle(
        GetSampleRequestSummaryQuery request,
        CancellationToken cancellationToken)    
    {
        var sampleRequestQuery = await SampleRequestSummaryQueryRules.BuildAsync(
            _dbContext, _visibilityService, request, cancellationToken);

        var sortedQuery = ApplySorting(sampleRequestQuery, request);

        var dtoQuery = sortedQuery.Select(sampleRequest => new SampleRequestSummaryProjection
        {
            CompanyId = sampleRequest.CompanyId,
            ProductId = sampleRequest.ProductId,
            AttachmentCollectionId = sampleRequest.AttachmentCollectionId,
            Summary = new SampleRequestSummaryDto
            {
                SampleRequestId = sampleRequest.SampleRequestId,
                ExternalId = sampleRequest.ExternalId,
                ProductId = sampleRequest.ProductId,

                ProductName = sampleRequest.Product.Name,
                CategoryId = sampleRequest.Product.CategoryId,
                CategoryExternalId = sampleRequest.Product.Category != null
                    ? sampleRequest.Product.Category.ExternalId
                    : null,
                CategoryType = sampleRequest.Product.Category != null
                    ? sampleRequest.Product.Category.Types
                    : null,
                CategoryName = sampleRequest.Product.Category != null
                    ? sampleRequest.Product.Category.Name
                    : null,
                ColorValue = sampleRequest.Product.ColourName,
                ColorDisplayName = sampleRequest.Product.ColourName,
                AdditiveCode = sampleRequest.Product.Additive,
                AdditiveGroupCode = SampleRequestAdditiveHelper.ResolveGroupCode(sampleRequest.Product.Additive),
                AdditiveDisplayName = sampleRequest.Product.Additive,
                ColourCode = sampleRequest.Product.ColourCode,

                Status = sampleRequest.Status,
                LabDeclineReason = sampleRequest.Status == "LabDeclined"
                    ? _dbContext.SampleRequestWorkflowEvents.Where(e => e.CompanyId == sampleRequest.CompanyId &&
                        e.SampleRequestId == sampleRequest.SampleRequestId && e.EventType == "SampleRequestLabDeclined")
                        .OrderByDescending(e => e.RecordedAt).ThenByDescending(e => e.Id).Select(e => e.Note).FirstOrDefault() : null,

                CustomerName = sampleRequest.Customer.CustomerName,
                CustomerExternalId = sampleRequest.Customer.ExternalId,

                CreatedBy = sampleRequest.CreatedByNavigation != null
                    ? sampleRequest.CreatedByNavigation.FullName
                    : "-",

                LabName = sampleRequest.Product.CreatedByNavigation != null
                    ? sampleRequest.Product.CreatedByNavigation.FullName
                    : "-",

                EndUserName = sampleRequest.Product.EndUser,

                ProductCreatedDate = sampleRequest.Product.CreatedDate,
                ProductUpdatedDate = sampleRequest.Product.UpdatedDate,

                SampleRequestCreatedDate = sampleRequest.CreatedDate,
                SampleRequestUpdatedDate = sampleRequest.UpdatedDate,

                ExpectedDeliveryDate = sampleRequest.ExpectedDeliveryDate,
                RequestDeliveryDate = sampleRequest.RequestDeliveryDate,
                LabReceivedDate = sampleRequest.LabReceivedDate,
                IsDelayed = sampleRequest.IsDelayed == true,
                LabReceivedByEmployeeId = sampleRequest.LabReceivedByEmployeeId,
                LabReceivedByName = sampleRequest.LabReceivedByNavigation != null
                    ? sampleRequest.LabReceivedByNavigation.FullName
                    : null,
                RealDeliveryDate = sampleRequest.RealDeliveryDate,
                RealPriceQuoteDate = sampleRequest.RealPriceQuoteDate,
                ExpectedPriceQuoteDate = sampleRequest.ExpectedPriceQuoteDate
            }
        });

    var totalCount = await sortedQuery.CountAsync(cancellationToken);

        var pagedRows = await dtoQuery
            .Skip((request.NormalizedPageNumber - 1) * request.NormalizedPageSize)
            .Take(request.NormalizedPageSize)
            .ToListAsync(cancellationToken);

        // Menu capability uses the same owner/team policy as the workflow mutation.
        // The dialog loads authoritative current plan actions (pending delivery, tokens, etc.).
        if (_user is not null && _permissions is not null)
        {
            var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
            var owners = SampleSaleWorkflowAccess.GetAllowedOwnerIds(_user, _permissions, scope);
            var companyWide = SampleSaleWorkflowAccess.HasCompanyWideAccess(_user, scope);
            var pageIds = pagedRows.Select(x => x.Summary.SampleRequestId).ToArray();
            var allowed = await _visibilityService.ApplySampleRequestVisibility(_dbContext.SampleRequests, _dbContext.Customers.AsNoTracking(), scope)
                .Where(x => pageIds.Contains(x.SampleRequestId) && x.CompanyId == scope.CompanyId && x.IsActive &&
                    (companyWide || owners.Contains(x.ManagerBy)) && _dbContext.Employees.Any(e => e.CompanyId == scope.CompanyId && e.EmployeeId == scope.EmployeeId && e.IsActive))
                .Select(x => x.SampleRequestId).ToArrayAsync(cancellationToken);
            foreach (var row in pagedRows) row.Summary.CanRequestAdditionalWork = allowed.Contains(row.Summary.SampleRequestId);
        }
        var canViewFormulaPrices = _fieldVisibility.CanViewFormulaPrices();

        await SampleRequestSummaryRelatedDataLoader.PopulateAsync(
            _dbContext,
            pagedRows,
            canViewFormulaPrices,
            cancellationToken);

        return new PagedResult<SampleRequestSummaryDto>(
            pagedRows.Select(x => x.Summary).ToList(),
            totalCount,
            request.NormalizedPageNumber,
            request.NormalizedPageSize);
    }

    private static IQueryable<SampleRequest> ApplySorting(
        IQueryable<SampleRequest> query,
        GetSampleRequestSummaryQuery request)
    {
        return request.NormalizedSortBy switch
        {
            SampleRequestSummarySortFeilds.ExternalId => request.SortDescending
                ? query.OrderByDescending(x => x.ExternalId).ThenByDescending(x => x.CreatedDate)
                : query.OrderBy(x => x.ExternalId).ThenByDescending(x => x.CreatedDate),

            SampleRequestSummarySortFeilds.ColourCode => request.SortDescending
                ? query.OrderByDescending(x => x.Product.ColourCode).ThenByDescending(x => x.CreatedDate)
                : query.OrderBy(x => x.Product.ColourCode).ThenByDescending(x => x.CreatedDate),

            SampleRequestSummarySortFeilds.ProductName => request.SortDescending
                ? query.OrderByDescending(x => x.Product.Name).ThenByDescending(x => x.CreatedDate)
                : query.OrderBy(x => x.Product.Name).ThenByDescending(x => x.CreatedDate),

            SampleRequestSummarySortFeilds.UpdatedDate => request.SortDescending
                ? query.OrderByDescending(x => x.UpdatedDate).ThenByDescending(x => x.CreatedDate)
                : query.OrderBy(x => x.UpdatedDate).ThenByDescending(x => x.CreatedDate),

            SampleRequestSummarySortFeilds.CreatedDate => request.SortDescending
                ? query.OrderByDescending(x => x.CreatedDate).ThenByDescending(x => x.UpdatedDate)
                : query.OrderBy(x => x.CreatedDate).ThenByDescending(x => x.UpdatedDate),

            _ => query
                .OrderBy(x => x.CreatedDate)
        };
    }

}
