using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.CustomerCare.Queries.GetCustomerTransferTargetEmployees;

/// <summary>
/// Tìm sale nhận hợp lệ cho chuyển giao, tách khỏi customer lookup để FE search nhẹ hơn.
/// </summary>
public sealed class GetCustomerTransferTargetEmployeesQuery
    : PaginationQuery, IRequest<OperationResult<PagedResult<CustomerTransferEmployeeOptionDto>>>;

internal sealed class GetCustomerTransferTargetEmployeesQueryHandler
    : IRequestHandler<GetCustomerTransferTargetEmployeesQuery, OperationResult<PagedResult<CustomerTransferEmployeeOptionDto>>>
{
    private readonly ICRMReadDbContext _dbContext;
    private readonly ICustomerVisibilityService _visibilityService;

    public GetCustomerTransferTargetEmployeesQueryHandler(
        ICRMReadDbContext dbContext,
        ICustomerVisibilityService visibilityService)
    {
        _dbContext = dbContext;
        _visibilityService = visibilityService;
    }

    public async Task<OperationResult<PagedResult<CustomerTransferEmployeeOptionDto>>> Handle(
        GetCustomerTransferTargetEmployeesQuery request,
        CancellationToken cancellationToken)
    {
        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
        if (!CustomerTransferRules.CanTransfer(scope))
        {
            return OperationResult<PagedResult<CustomerTransferEmployeeOptionDto>>.Fail(
                CustomerTransferRules.TransferPermissionMessage);
        }

        var query = _dbContext.MemberInGroups
            .AsNoTracking()
            .Where(member =>
                member.IsActive &&
                member.Profile.HasValue &&
                member.ProfileNavigation != null &&
                member.ProfileNavigation.IsActive &&
                member.ProfileNavigation.CompanyId == scope.CompanyId &&
                member.Group.CompanyId == scope.CompanyId);

        if (!scope.HasFullCustomerView)
        {
            var leaderGroupIds = scope.LeaderGroupIds.ToArray();
            query = query.Where(member => leaderGroupIds.Contains(member.GroupId));
        }

        if (!string.IsNullOrWhiteSpace(request.NormalizedKeyword))
        {
            var keyword = request.NormalizedKeyword;
            query = query.Where(member =>
                member.ProfileNavigation!.FullName.Contains(keyword) ||
                (member.Group.Name != null && member.Group.Name.Contains(keyword)));
        }

        var materialized = await query
            .GroupBy(member => new
            {
                EmployeeId = member.Profile!.Value,
                EmployeeName = member.ProfileNavigation!.FullName,
                member.GroupId,
                GroupName = member.Group.Name
            })
            .OrderBy(x => x.Key.EmployeeName)
            .ThenBy(x => x.Key.GroupName)
            .Select(x => new CustomerTransferEmployeeOptionDto
            {
                EmployeeId = x.Key.EmployeeId,
                EmployeeName = x.Key.EmployeeName,
                GroupId = x.Key.GroupId,
                GroupName = x.Key.GroupName
            })
            .ToListAsync(cancellationToken);

        var pageNumber = request.NormalizedPageNumber;
        var pageSize = request.NormalizedPageSize;
        var pageItems = materialized
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return OperationResult<PagedResult<CustomerTransferEmployeeOptionDto>>.Ok(
            new PagedResult<CustomerTransferEmployeeOptionDto>(
                pageItems,
                materialized.Count,
                pageNumber,
                pageSize));
    }
}
