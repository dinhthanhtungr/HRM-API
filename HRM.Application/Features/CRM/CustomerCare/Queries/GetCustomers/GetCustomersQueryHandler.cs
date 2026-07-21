using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.WorkTaskEnums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.CustomerCare.Queries.GetCustomers;

internal sealed class GetCustomersQueryHandler
    : IRequestHandler<GetCustomersQuery, PagedResult<CustomerListItemDto>>
{
    private readonly ICustomerVisibilityReadDbContext _dbContext;
    private readonly ICustomerVisibilityService _visibilityService;

    public GetCustomersQueryHandler(
        ICustomerVisibilityReadDbContext dbContext,
        ICustomerVisibilityService visibilityService)
    {
        _dbContext = dbContext;
        _visibilityService = visibilityService;
    }

    public async Task<PagedResult<CustomerListItemDto>> Handle(
        GetCustomersQuery request,
        CancellationToken cancellationToken)
    {
        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
        var now = scope.Now;
        var leaderGroupIds = scope.LeaderGroupIds.ToArray();
        var restrictAssignmentsToLeaderGroups = !scope.HasFullCustomerView && leaderGroupIds.Length > 0;

        var query = _dbContext.Customers
            .AsNoTracking()
            .AsQueryable();

        query = _visibilityService.ApplyCustomerVisibility(query, scope);

        if (request.CompanyId is { } companyId && companyId != Guid.Empty)
        {
            query = query.Where(x => x.CompanyId == companyId);
        }

        if (request.SaleEmployeeId is { } saleId && saleId != Guid.Empty)
        {
            query = query.Where(x =>
                x.CurrentSaleId == saleId ||
                x.CustomerAssignments.Any(a => a.EmployeeId == saleId && a.IsActive) ||
                x.CustomerClaims.Any(cl =>
                    cl.EmployeeId == saleId &&
                    cl.IsActive &&
                    cl.Type == ClaimType.Work &&
                    cl.ExpiresAt > now));
        }

        if (request.GroupId is { } groupId && groupId != Guid.Empty)
        {
            query = query.Where(x =>
                x.CustomerAssignments.Any(a => a.GroupId == groupId && a.IsActive) ||
                x.CustomerClaims.Any(cl =>
                    cl.GroupId == groupId &&
                    cl.IsActive &&
                    cl.Type == ClaimType.Work &&
                    cl.ExpiresAt > now));
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

        if (request.IsActive.HasValue)
        {
            query = query.Where(x => x.IsActive == request.IsActive.Value);
        }

        if (request.NextFollowUpFrom.HasValue)
        {
            var from = request.NextFollowUpFrom.Value.Date;
            query = query.Where(x => x.NextFollowUpDate >= from);
        }

        if (request.NextFollowUpTo.HasValue)
        {
            var toExclusive = request.NextFollowUpTo.Value.Date.AddDays(1);
            query = query.Where(x => x.NextFollowUpDate < toExclusive);
        }

        if (!string.IsNullOrWhiteSpace(request.NormalizedKeyword))
        {
            var keyword = request.NormalizedKeyword;

            query = query.Where(x =>
                x.ExternalId.Contains(keyword) ||
                x.CustomerName.Contains(keyword) ||
                (x.CustomerGroup ?? string.Empty).Contains(keyword) ||
                (x.ApplicationName ?? string.Empty).Contains(keyword) ||
                (x.Phone ?? string.Empty).Contains(keyword) ||
                (x.TaxNumber ?? string.Empty).Contains(keyword));
        }

        query = ApplySorting(query, request);

        var projected = query
            .Select(x => new
            {
                Customer = x,
                HasActiveAssignment = x.CustomerAssignments.Any(a => a.IsActive),
                LatestAssignment = x.CustomerAssignments
                    .Where(a =>
                        a.IsActive &&
                        (!restrictAssignmentsToLeaderGroups || leaderGroupIds.Contains(a.GroupId)))
                    .OrderByDescending(a => a.CreatedDate)
                    .Select(a => new
                    {
                        EmployeeId = (Guid?)a.EmployeeId,
                        EmployeeName = a.Employee.FullName
                    })
                    .FirstOrDefault(),
                LatestClaim = x.CustomerClaims
                    .Where(cl =>
                        cl.IsActive &&
                        cl.Type == ClaimType.Work &&
                        cl.ExpiresAt > now)
                    .OrderByDescending(cl => cl.ExpiresAt)
                    .Select(cl => new
                    {
                        EmployeeId = (Guid?)cl.EmployeeId,
                        EmployeeName = cl.Employee.FullName,
                        cl.ExpiresAt
                    })
                    .FirstOrDefault(),
                CurrentSaleName = _dbContext.Employees
                    .Where(employee => x.CurrentSaleId.HasValue && employee.EmployeeId == x.CurrentSaleId.Value)
                    .Select(employee => employee.FullName)
                    .FirstOrDefault()
            })
            .Select(x => new CustomerListItemDto
            {
                CustomerId = x.Customer.CustomerId,
                ExternalId = x.Customer.ExternalId,
                CustomerName = x.Customer.CustomerName,
                IsLead = x.Customer.IsLead,
                LeadStatus = x.Customer.LeadStatus.ToString(),
                TaxNumber = x.Customer.TaxNumber ?? "_",

                CustomerGroup = x.Customer.CustomerGroup,
                ApplicationName = x.Customer.ApplicationName,
                Phone = x.Customer.Phone,

                CurrentCrmStatus = x.Customer.CurrentCrmStatus,
                CurrentSaleId = x.Customer.IsLead && !x.HasActiveAssignment
                    ? x.LatestClaim != null ? x.LatestClaim.EmployeeId : x.Customer.CurrentSaleId
                    : x.LatestAssignment != null ? x.LatestAssignment.EmployeeId : x.Customer.CurrentSaleId,
                CurrentSaleName = x.Customer.IsLead && !x.HasActiveAssignment
                    ? x.LatestClaim != null ? x.LatestClaim.EmployeeName : x.CurrentSaleName
                    : x.LatestAssignment != null ? x.LatestAssignment.EmployeeName : x.CurrentSaleName,

                LastContactDate = x.Customer.LastContactDate,
                NextFollowUpDate = x.Customer.NextFollowUpDate,
                endLeadTime = x.Customer.IsLead && !x.HasActiveAssignment && x.LatestClaim != null
                    ? x.LatestClaim.ExpiresAt
                    : null,

                OpenTaskCount = _dbContext.CustomerInteractions.Count(task =>
                    task.CompanyId == x.Customer.CompanyId &&
                    task.IsActive &&
                    task.CustomerId == x.Customer.CustomerId),

                CreatedDate = x.Customer.CreatedDate,
                IsActive = x.Customer.IsActive
            });

        return await projected.ToPagedResultAsync(
            request.NormalizedPageNumber,
            request.NormalizedPageSize,
            cancellationToken);
    }

    private static IQueryable<Customer> ApplySorting(
        IQueryable<Customer> query,
        GetCustomersQuery request)
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
