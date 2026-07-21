using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.CustomerCare.Queries.GetCustomerTransfers;

/// <summary>
/// Feature CRM CustomerCare - xử lý đọc log chuyển giao không làm lộ customer ngoài scope.
/// Handler chỉ trả log có ít nhất một customer visible và chỉ hydrate danh sách customer visible trong log.
/// </summary>
internal sealed class GetCustomerTransfersQueryHandler
    : IRequestHandler<GetCustomerTransfersQuery, PagedResult<CustomerTransferLogDto>>
{
    private readonly ICRMReadDbContext _dbContext;
    private readonly ICustomerVisibilityService _visibilityService;

    public GetCustomerTransfersQueryHandler(
        ICRMReadDbContext dbContext,
        ICustomerVisibilityService visibilityService)
    {
        _dbContext = dbContext;
        _visibilityService = visibilityService;
    }

    public async Task<PagedResult<CustomerTransferLogDto>> Handle(
        GetCustomerTransfersQuery request,
        CancellationToken cancellationToken)
    {
        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
        var visibleCustomerIds = _visibilityService
            .ApplyCustomerVisibility(_dbContext.Customers.AsNoTracking(), scope)
            .Where(customer => customer.CustomerId != CustomerVisibilityConstants.RestrictedCustomerId)
            .Select(customer => customer.CustomerId);

        var query = _dbContext.CustomerTransferLogs
            .AsNoTracking()
            .Where(log =>
                log.CompanyId == scope.CompanyId &&
                log.DetailCustomerTransfers.Any(detail => visibleCustomerIds.Contains(detail.CustomerId)));

        if (request.CustomerId is { } customerId && customerId != Guid.Empty)
        {
            query = query.Where(log =>
                log.DetailCustomerTransfers.Any(detail =>
                    detail.CustomerId == customerId &&
                    visibleCustomerIds.Contains(detail.CustomerId)));
        }

        if (request.FromEmployeeId is { } fromEmployeeId && fromEmployeeId != Guid.Empty)
        {
            query = query.Where(log => log.FromEmployeeId == fromEmployeeId);
        }

        if (request.ToEmployeeId is { } toEmployeeId && toEmployeeId != Guid.Empty)
        {
            query = query.Where(log => log.ToEmployeeId == toEmployeeId);
        }

        if (request.TransferType.HasValue)
        {
            query = query.Where(log => log.TransferType == request.TransferType.Value);
        }

        if (request.CreatedFrom.HasValue)
        {
            query = query.Where(log => log.CreatedDate >= request.CreatedFrom.Value);
        }

        if (request.CreatedTo.HasValue)
        {
            query = query.Where(log => log.CreatedDate <= request.CreatedTo.Value);
        }

        var keyword = request.NormalizedKeyword;
        if (keyword is not null)
        {
            query = query.Where(log =>
                (log.Note ?? string.Empty).Contains(keyword) ||
                log.FromEmployee.FullName.Contains(keyword) ||
                log.ToEmployee.FullName.Contains(keyword) ||
                log.DetailCustomerTransfers.Any(detail =>
                    visibleCustomerIds.Contains(detail.CustomerId) &&
                    (detail.Customer.CustomerName.Contains(keyword) ||
                     detail.Customer.ExternalId.Contains(keyword))));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var pageNumber = request.NormalizedPageNumber;
        var pageSize = request.NormalizedPageSize;

        var rows = await query
            .OrderByDescending(log => log.CreatedDate)
            .ThenByDescending(log => log.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(log => new CustomerTransferLogDto
            {
                TransferLogId = log.Id,
                TransferType = log.TransferType,
                CreatedDate = log.CreatedDate,
                CreatedBy = log.CreatedBy,
                CreatedByName = log.CreatedByNavigation.FullName,
                FromEmployeeId = log.FromEmployeeId,
                FromEmployeeName = log.FromEmployee.FullName,
                ToEmployeeId = log.ToEmployeeId,
                ToEmployeeName = log.ToEmployee.FullName,
                FromGroupId = log.FromGroupId,
                FromGroupName = log.FromGroup.Name,
                ToGroupId = log.ToGroupId,
                ToGroupName = log.ToGroup.Name,
                Note = log.Note,
                CustomerCount = log.DetailCustomerTransfers.Count(detail =>
                    visibleCustomerIds.Contains(detail.CustomerId)),
                Customers = log.DetailCustomerTransfers
                    .Where(detail => visibleCustomerIds.Contains(detail.CustomerId))
                    .OrderBy(detail => detail.Customer.CustomerName)
                    .Select(detail => new CustomerTransferLogCustomerDto
                    {
                        CustomerId = detail.CustomerId,
                        ExternalId = detail.Customer.ExternalId,
                        CustomerName = detail.Customer.CustomerName,
                        IsLead = detail.Customer.IsLead,
                        LeadStatus = detail.Customer.LeadStatus.ToString()
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<CustomerTransferLogDto>(rows, totalCount, pageNumber, pageSize);
    }
}
