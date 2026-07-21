using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using HRM.Application.Features.CRM.CustomerCare.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.CustomerCare.Queries.GetCustomerInteractionsByCustomer;

internal sealed class GetCustomerInteractionsByCustomerQueryHandler
    : IRequestHandler<GetCustomerInteractionsByCustomerQuery, OperationResult<PagedResult<CustomerInteractionDto>>>
{
    private readonly ICRMReadDbContext _dbContext;
    private readonly CustomerCrmAccessService _accessService;

    public GetCustomerInteractionsByCustomerQueryHandler(
        ICRMReadDbContext dbContext,
        CustomerCrmAccessService accessService)
    {
        _dbContext = dbContext;
        _accessService = accessService;
    }

    public async Task<OperationResult<PagedResult<CustomerInteractionDto>>> Handle(
        GetCustomerInteractionsByCustomerQuery request,
        CancellationToken cancellationToken)
    {
        var scope = await _accessService.BuildScopeAsync(cancellationToken);
        if (!await _accessService.VisibleCustomers(scope).AnyAsync(x => x.CustomerId == request.CustomerId, cancellationToken))
        {
            return OperationResult<PagedResult<CustomerInteractionDto>>.Fail("Customer was not found.");
        }

        var query = request.Query;
        var source = ProjectInteractions().Where(x => x.CustomerId == request.CustomerId);
        if (!query.IncludeInactive) source = source.Where(x => x.IsActive);
        if (query.AssignedSaleEmployeeId.HasValue) source = source.Where(x => x.AssignedSaleEmployeeId == query.AssignedSaleEmployeeId);
        if (query.InteractionType.HasValue) source = source.Where(x => x.InteractionType == query.InteractionType);
        if (query.From.HasValue) source = source.Where(x => x.InteractionAt >= query.From.Value);
        if (query.To.HasValue) source = source.Where(x => x.InteractionAt < query.To.Value.Date.AddDays(1));
        if (!string.IsNullOrWhiteSpace(query.NormalizedKeyword))
        {
            var keyword = query.NormalizedKeyword;
            source = source.Where(x =>
                (x.Subject ?? string.Empty).Contains(keyword) ||
                x.Content.Contains(keyword) ||
                (x.Outcome ?? string.Empty).Contains(keyword));
        }

        var total = await source.CountAsync(cancellationToken);
        var items = await source
            .OrderByDescending(x => x.InteractionAt)
            .ThenByDescending(x => x.InteractionId)
            .Skip((query.NormalizedPageNumber - 1) * query.NormalizedPageSize)
            .Take(query.NormalizedPageSize)
            .ToListAsync(cancellationToken);

        return OperationResult<PagedResult<CustomerInteractionDto>>.Ok(
            new PagedResult<CustomerInteractionDto>(items, total, query.NormalizedPageNumber, query.NormalizedPageSize));
    }

    private IQueryable<CustomerInteractionDto> ProjectInteractions()
        => _dbContext.CustomerInteractions.AsNoTracking().Select(x => new CustomerInteractionDto
        {
            InteractionId = x.Id,
            CustomerId = x.CustomerId,
            CustomerExternalId = x.Customer.ExternalId,
            CustomerName = x.Customer.CustomerName,
            ContactId = x.ContactId,
            InteractionType = x.InteractionType,
            Subject = x.Subject,
            Content = x.Content,
            Outcome = x.Outcome,
            NextAction = x.NextAction,
            InteractionAt = x.InteractionAt,
            NextFollowUpDate = x.NextFollowUpDate,
            AssignedSaleEmployeeId = x.AssignedSaleEmployeeId,
            AssignedSaleEmployeeName = x.AssignedSaleEmployee != null ? x.AssignedSaleEmployee.FullName : null,
            IsActive = x.IsActive
        });
}
