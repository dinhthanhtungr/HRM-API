using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Domain.Enums.CustomerEnum;
using MediatR;

namespace HRM.Application.Features.CRM.CustomerCare.Queries.GetCustomerTransferSourceEmployees;

/// <summary>
/// Tìm sale nguồn có khách/lead hiện đang quản lý để FE không phải load kèm danh sách customer.
/// </summary>
public sealed class GetCustomerTransferSourceEmployeesQuery
    : PaginationQuery, IRequest<OperationResult<PagedResult<CustomerTransferEmployeeOptionDto>>>;

internal sealed class GetCustomerTransferSourceEmployeesQueryHandler
    : IRequestHandler<GetCustomerTransferSourceEmployeesQuery, OperationResult<PagedResult<CustomerTransferEmployeeOptionDto>>>
{
    private readonly ICRMReadDbContext _dbContext;
    private readonly ICustomerVisibilityService _visibilityService;

    public GetCustomerTransferSourceEmployeesQueryHandler(
        ICRMReadDbContext dbContext,
        ICustomerVisibilityService visibilityService)
    {
        _dbContext = dbContext;
        _visibilityService = visibilityService;
    }

    public async Task<OperationResult<PagedResult<CustomerTransferEmployeeOptionDto>>> Handle(
        GetCustomerTransferSourceEmployeesQuery request,
        CancellationToken cancellationToken)
    {
        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
        if (!CustomerTransferRules.CanTransfer(scope))
        {
            return OperationResult<PagedResult<CustomerTransferEmployeeOptionDto>>.Fail(
                CustomerTransferRules.TransferPermissionMessage);
        }

        var owners = await CustomerTransferRules.BuildVisibleOwnerRowsAsync(
            _dbContext,
            _visibilityService,
            scope,
            cancellationToken);

        var query = owners
            .GroupBy(x => new { x.SourceEmployeeId, x.SourceEmployeeName, x.SourceGroupId, x.SourceGroupName })
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
            .AsEnumerable();

        if (!string.IsNullOrWhiteSpace(request.NormalizedKeyword))
        {
            var keyword = request.NormalizedKeyword;
            query = query.Where(x =>
                x.EmployeeName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                (x.GroupName?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        var materialized = query
            .OrderBy(x => x.EmployeeName)
            .ThenBy(x => x.GroupName)
            .ToList();

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
