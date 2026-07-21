using HRM.Application.Abstractions.Persistence.Employees;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.Employees.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Employees.Queries.GetGroupLookup;

internal sealed class GetGroupLookupQueryHandler
    : IRequestHandler<GetGroupLookupQuery, PagedResult<GroupLookupDto>>
{
    private readonly IEmployeeReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public GetGroupLookupQueryHandler(
        IEmployeeReadDbContext dbContext,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<GroupLookupDto>> Handle(
        GetGroupLookupQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated)
        {
            throw new UnauthorizedAccessException();
        }

        var companyId = _currentUser.CompanyId
            ?? throw new UnauthorizedAccessException("Current user has no CompanyId.");

        var employeeId = _currentUser.EmployeeId
            ?? throw new UnauthorizedAccessException("Current user has no EmployeeId.");

        var query = _dbContext.Groups
            .AsNoTracking()
            .Where(group => group.CompanyId == companyId);

        if (!string.IsNullOrWhiteSpace(request.GroupType))
        {
            var groupType = request.GroupType.Trim();
            query = query.Where(group => group.GroupType == groupType);
        }

        if (!string.IsNullOrWhiteSpace(request.GroupTypePrefix))
        {
            var prefix = request.GroupTypePrefix.Trim();
            query = query.Where(group =>
                group.GroupType == prefix ||
                group.GroupType != null && group.GroupType.StartsWith(prefix + "."));
        }

        if (request.NormalizedKeyword is { } keyword)
        {
            query = query.Where(group =>
                group.ExternalId.Contains(keyword) ||
                (group.Name ?? string.Empty).Contains(keyword) ||
                (group.GroupType ?? string.Empty).Contains(keyword));
        }


        if (!HasFullGroupLookupAccess())
        {
            query = query.Where(group =>
                _dbContext.MemberInGroups.Any(member =>
                    member.GroupId == group.GroupId &&
                    member.Profile == employeeId &&
                    member.IsActive &&
                    member.IsAdmin == true));
        }

        var projected = query
            .OrderBy(group => group.GroupType)
            .ThenBy(group => group.Name)
            .ThenBy(group => group.ExternalId)
            .Select(group => new GroupLookupDto
            {
                GroupId = group.GroupId,
                ExternalId = group.ExternalId,
                Name = group.Name,
                GroupType = group.GroupType,
                CompanyId = group.CompanyId
            });

        return await projected.ToPagedResultAsync(
            request.NormalizedPageNumber,
            request.NormalizedPageSize,
            cancellationToken);
    }

    private bool HasFullGroupLookupAccess()
        => _currentUser.IsInRole(ApplicationRoles.Admin) ||
           _currentUser.IsInRole(ApplicationRoles.President) ||
           _currentUser.IsInRole(ApplicationRoles.Developer) ||
           _currentUser.IsInRole(ApplicationRoles.Sales.CustomerViewAll);
}
