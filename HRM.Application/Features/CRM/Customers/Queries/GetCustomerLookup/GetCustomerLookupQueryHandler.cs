using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.CRM.CustomerCare.Queries.GetCustomers;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.CRM.Customers.Dtos.GetCustomerLookup;
using HRM.Application.Commons.Rules;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.CustomerEnum;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Customers.Queries.GetCustomerLookup;

internal sealed class GetCustomerLookupQueryHandler
    : IRequestHandler<GetCustomerLookupQuery, PagedResult<CustomerLookupDto>>
{
    private readonly ICustomerVisibilityReadDbContext _dbContext;
    private readonly ICustomerVisibilityService _visibilityService;

    public GetCustomerLookupQueryHandler(
        ICustomerVisibilityReadDbContext dbContext,
        ICustomerVisibilityService visibilityService)
    {
        _dbContext = dbContext;
        _visibilityService = visibilityService;
    }

    public async Task<PagedResult<CustomerLookupDto>> Handle(
        GetCustomerLookupQuery request,
        CancellationToken cancellationToken)
    {
        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
        var now = scope.Now;

        var query = _dbContext.Customers
            .AsNoTracking()
            .AsQueryable();

        var visibleCustomerIds = _visibilityService.ApplyCustomerVisibility(query, scope)
            .Select(x => x.CustomerId);
        query = request.IncludeInternalForSaleOrder
            ? query.Where(x =>
                x.CompanyId == scope.CompanyId &&
                x.IsActive == true &&
                (visibleCustomerIds.Contains(x.CustomerId) ||
                 x.ExternalId == InternalCustomerRules.InternalCustomerExternalId))
            : query.Where(x => visibleCustomerIds.Contains(x.CustomerId));

        if (request.CompanyId is { } companyId && companyId != Guid.Empty)
        {
            query = query.Where(x => x.CompanyId == companyId);
        }

        if (request.SaleEmployeeId is { } saleId && saleId != Guid.Empty)
        {
            query = query.Where(x =>
                x.CurrentSaleId == saleId ||
                x.CustomerAssignments.Any(assignment =>
                    assignment.EmployeeId == saleId &&
                    assignment.IsActive) ||
                x.CustomerClaims.Any(claim =>
                    claim.EmployeeId == saleId &&
                    claim.IsActive &&
                    claim.Type == ClaimType.Work &&
                    claim.ExpiresAt > now));
        }

        if (request.GroupId is { } groupId && groupId != Guid.Empty)
        {
            query = query.Where(x =>
                x.CustomerAssignments.Any(assignment =>
                    assignment.GroupId == groupId &&
                    assignment.IsActive) ||
                x.CustomerClaims.Any(claim =>
                    claim.GroupId == groupId &&
                    claim.IsActive &&
                    claim.Type == ClaimType.Work &&
                    claim.ExpiresAt > now));
        }

        if (request.IsActive.HasValue)
        {
            query = query.Where(x => x.IsActive == request.IsActive.Value);
        }

        if (request.CustomerId is { } customerId && customerId != Guid.Empty)
        {
            query = query.Where(x => x.CustomerId == customerId);
        }

        if (request.IsLead.HasValue)
        {
            query = query.Where(x => x.IsLead == request.IsLead.Value);
        }

        if (request.LeadStatus.HasValue)
        {
            query = query.Where(x => x.LeadStatus == request.LeadStatus.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.CurrentCrmStatus))
        {
            var crmStatus = request.CurrentCrmStatus.Trim();
            query = query.Where(x => x.CurrentCrmStatus == crmStatus);
        }

        if (!string.IsNullOrWhiteSpace(request.NormalizedKeyword))
        {
            var keyword = request.NormalizedKeyword;

            query = query.Where(x =>
                x.ExternalId.Contains(keyword) ||
                x.CustomerName.Contains(keyword) ||
                x.CustomerAssignments.Any(a =>
                    a.IsActive &&
                    a.Employee.FullName.Contains(keyword)) ||
                (x.CustomerGroup ?? string.Empty).Contains(keyword) ||
                (x.ApplicationName ?? string.Empty).Contains(keyword) ||
                (x.Phone ?? string.Empty).Contains(keyword) ||
                (x.TaxNumber ?? string.Empty).Contains(keyword));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        query = ApplySorting(query, request);

        var result = await query
            .Skip((request.NormalizedPageNumber - 1) * request.NormalizedPageSize)
            .Take(request.NormalizedPageSize)
            .Select(x => new CustomerLookupDto
            {
                CustomerId = x.CustomerId,
                IsLead = x.IsLead,
                ExternalId = x.ExternalId,
                CustomerName = x.CustomerName
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<CustomerLookupDto>(
            result,
            totalCount,
            request.NormalizedPageNumber,
            request.NormalizedPageSize);
    }

    private static IQueryable<Customer> ApplySorting(
        IQueryable<Customer> query,
        GetCustomerLookupQuery request)
    {
        return request.NormalizedSortBy switch
        {
            CustomerSortFields.ExternalId => request.SortDescending
                ? query.OrderByDescending(x => x.ExternalId).ThenByDescending(x => x.CustomerId)
                : query.OrderBy(x => x.ExternalId).ThenByDescending(x => x.CustomerId),

            CustomerSortFields.CustomerName => request.SortDescending
                ? query.OrderByDescending(x => x.CustomerName).ThenByDescending(x => x.CustomerId)
                : query.OrderBy(x => x.CustomerName).ThenByDescending(x => x.CustomerId),

            CustomerSortFields.LeadStatus => request.SortDescending
                ? query.OrderByDescending(x => x.LeadStatus).ThenByDescending(x => x.CustomerId)
                : query.OrderBy(x => x.LeadStatus).ThenByDescending(x => x.CustomerId),

            CustomerSortFields.CurrentCrmStatus => request.SortDescending
                ? query.OrderByDescending(x => x.CurrentCrmStatus).ThenByDescending(x => x.CustomerId)
                : query.OrderBy(x => x.CurrentCrmStatus).ThenByDescending(x => x.CustomerId),

            CustomerSortFields.LastContactDate => request.SortDescending
                ? query.OrderByDescending(x => x.LastContactDate).ThenByDescending(x => x.CustomerId)
                : query.OrderBy(x => x.LastContactDate).ThenByDescending(x => x.CustomerId),

            CustomerSortFields.NextFollowUpDate => request.SortDescending
                ? query.OrderByDescending(x => x.NextFollowUpDate).ThenByDescending(x => x.CustomerId)
                : query.OrderBy(x => x.NextFollowUpDate).ThenByDescending(x => x.CustomerId),

            CustomerSortFields.CreatedDate => request.SortDescending
                ? query.OrderByDescending(x => x.CreatedDate).ThenByDescending(x => x.CustomerId)
                : query.OrderBy(x => x.CreatedDate).ThenByDescending(x => x.CustomerId),

            _ => query
                .OrderBy(x => x.NextFollowUpDate == null)
                .ThenBy(x => x.NextFollowUpDate)
                .ThenByDescending(x => x.LastContactDate)
                .ThenBy(x => x.CustomerName)
        };
    }
}
