using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Domain.Enums.CustomerEnum;
using MediatR;

namespace HRM.Application.Features.CRM.CustomerCare.Queries.GetCustomerTransferCustomers;

/// <summary>
/// Tìm khách/lead nguồn để chuyển giao, tách khỏi lookup sale để FE search không reload cả màn.
/// </summary>
public sealed class GetCustomerTransferCustomersQuery
    : PaginationQuery, IRequest<OperationResult<CustomerTransferCustomerLookupDto>>
{
    public Guid? SourceEmployeeId { get; init; }
    public Guid? SourceCustomerId { get; init; }
}

internal sealed class GetCustomerTransferCustomersQueryHandler
    : IRequestHandler<GetCustomerTransferCustomersQuery, OperationResult<CustomerTransferCustomerLookupDto>>
{
    private readonly ICRMReadDbContext _dbContext;
    private readonly ICustomerVisibilityService _visibilityService;

    public GetCustomerTransferCustomersQueryHandler(
        ICRMReadDbContext dbContext,
        ICustomerVisibilityService visibilityService)
    {
        _dbContext = dbContext;
        _visibilityService = visibilityService;
    }

    public async Task<OperationResult<CustomerTransferCustomerLookupDto>> Handle(
        GetCustomerTransferCustomersQuery request,
        CancellationToken cancellationToken)
    {
        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
        if (!CustomerTransferRules.CanTransfer(scope))
        {
            return OperationResult<CustomerTransferCustomerLookupDto>.Fail(
                CustomerTransferRules.TransferPermissionMessage);
        }

        var owners = await CustomerTransferRules.BuildVisibleOwnerRowsAsync(
            _dbContext,
            _visibilityService,
            scope,
            cancellationToken);

        var query = owners.AsEnumerable();
        if (request.SourceEmployeeId is { } sourceEmployeeId && sourceEmployeeId != Guid.Empty)
        {
            query = query.Where(x => x.SourceEmployeeId == sourceEmployeeId);
        }

        if (request.SourceCustomerId is { } sourceCustomerId && sourceCustomerId != Guid.Empty)
        {
            query = query.Where(x => x.CustomerId == sourceCustomerId);
        }

        if (!string.IsNullOrWhiteSpace(request.NormalizedKeyword))
        {
            var keyword = request.NormalizedKeyword;
            query = query.Where(x =>
                x.ExternalId.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                x.CustomerName.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        var materialized = query
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

        return OperationResult<CustomerTransferCustomerLookupDto>.Ok(new CustomerTransferCustomerLookupDto
        {
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
        });
    }
}
