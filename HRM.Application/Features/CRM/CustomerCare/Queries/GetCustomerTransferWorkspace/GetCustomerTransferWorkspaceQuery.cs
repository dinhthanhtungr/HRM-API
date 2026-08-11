using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.CRM.CustomerCare;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Domain.Enums.CustomerEnum;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.CustomerCare.Queries.GetCustomerTransferWorkspace;

/// <summary>
/// Lấy dữ liệu một lần cho màn chuyển giao khách hàng: nguồn, khách theo nguồn và nhân viên nhận.
/// </summary>
public sealed class GetCustomerTransferWorkspaceQuery
    : PaginationQuery, IRequest<OperationResult<CustomerTransferWorkspaceDto>>
{
    public Guid? SourceEmployeeId { get; init; }
    public Guid? SourceCustomerId { get; init; }
}

internal sealed class GetCustomerTransferWorkspaceQueryHandler
    : IRequestHandler<GetCustomerTransferWorkspaceQuery, OperationResult<CustomerTransferWorkspaceDto>>
{
    private readonly ICRMReadDbContext _dbContext;
    private readonly ICustomerVisibilityService _visibilityService;

    public GetCustomerTransferWorkspaceQueryHandler(
        ICRMReadDbContext dbContext,
        ICustomerVisibilityService visibilityService)
    {
        _dbContext = dbContext;
        _visibilityService = visibilityService;
    }

    public async Task<OperationResult<CustomerTransferWorkspaceDto>> Handle(
        GetCustomerTransferWorkspaceQuery request,
        CancellationToken cancellationToken)
    {
        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
        if (!CustomerTransferRules.CanTransfer(scope))
        {
            return OperationResult<CustomerTransferWorkspaceDto>.Fail(
                CustomerTransferRules.TransferPermissionMessage);
        }

        var owners = await CustomerTransferRules.BuildVisibleOwnerRowsAsync(
            _dbContext,
            _visibilityService,
            scope,
            cancellationToken);

        var filtered = owners.AsEnumerable();
        if (request.SourceEmployeeId is { } sourceEmployeeId && sourceEmployeeId != Guid.Empty)
        {
            filtered = filtered.Where(x => x.SourceEmployeeId == sourceEmployeeId);
        }

        if (request.SourceCustomerId is { } sourceCustomerId && sourceCustomerId != Guid.Empty)
        {
            filtered = filtered.Where(x => x.CustomerId == sourceCustomerId);
        }

        if (!string.IsNullOrWhiteSpace(request.NormalizedKeyword))
        {
            var keyword = request.NormalizedKeyword;
            filtered = filtered.Where(x =>
                x.ExternalId.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                x.CustomerName.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        var materialized = filtered
            .OrderBy(x => x.CustomerName)
            .ThenBy(x => x.CustomerId)
            .ToList();

        var pageNumber = request.NormalizedPageNumber;
        var pageSize = request.NormalizedPageSize;
        var pageItems = materialized
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(CustomerTransferRules.ToCustomerOption)
            .ToList();

        var resolvedSource = request.SourceCustomerId.HasValue && materialized.Count == 1
            ? CustomerTransferRules.ToOwnerDto(materialized[0])
            : null;

        var dto = new CustomerTransferWorkspaceDto
        {
            SourceEmployees = BuildEmployeeOptions(materialized),
            TargetEmployees = await BuildTargetEmployeeOptionsAsync(scope, cancellationToken),
            ResolvedSource = resolvedSource,
            Summary = new CustomerTransferWorkspaceSummaryDto
            {
                TotalCount = materialized.Count,
                LeadCount = materialized.Count(x => x.TransferType == TransferType.Lead),
                SaledCount = materialized.Count(x => x.TransferType == TransferType.Saled)
            },
            Customers = new PagedResult<CustomerTransferCustomerOptionDto>(
                pageItems,
                materialized.Count,
                pageNumber,
                pageSize)
        };

        return OperationResult<CustomerTransferWorkspaceDto>.Ok(dto);
    }

    private static IReadOnlyList<CustomerTransferEmployeeOptionDto> BuildEmployeeOptions(
        IReadOnlyCollection<CustomerTransferOwnerRow> owners)
        => owners
            .GroupBy(x => new { x.SourceEmployeeId, x.SourceEmployeeName, x.SourceGroupId, x.SourceGroupName })
            .OrderBy(x => x.Key.SourceEmployeeName)
            .Select(x => new CustomerTransferEmployeeOptionDto
            {
                EmployeeId = x.Key.SourceEmployeeId,
                EmployeeName = x.Key.SourceEmployeeName,
                GroupId = x.Key.SourceGroupId,
                GroupName = x.Key.SourceGroupName,
                CustomerCount = x.Count(),
                LeadCount = x.Count(customer => customer.TransferType == TransferType.Lead),
                SaledCount = x.Count(customer => customer.TransferType == TransferType.Saled)
            })
            .ToList();

    private async Task<IReadOnlyList<CustomerTransferEmployeeOptionDto>> BuildTargetEmployeeOptionsAsync(
        Commons.Authorization.ViewerScope scope,
        CancellationToken cancellationToken)
    {
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

        return await query
            .GroupBy(member => new
            {
                EmployeeId = member.Profile!.Value,
                EmployeeName = member.ProfileNavigation!.FullName,
                member.GroupId,
                GroupName = member.Group.Name
            })
            .OrderBy(x => x.Key.EmployeeName)
            .Select(x => new CustomerTransferEmployeeOptionDto
            {
                EmployeeId = x.Key.EmployeeId,
                EmployeeName = x.Key.EmployeeName,
                GroupId = x.Key.GroupId,
                GroupName = x.Key.GroupName
            })
            .ToListAsync(cancellationToken);
    }
}
