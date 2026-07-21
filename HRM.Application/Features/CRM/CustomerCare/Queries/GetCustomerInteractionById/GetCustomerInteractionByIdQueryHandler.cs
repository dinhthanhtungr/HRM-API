using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using HRM.Application.Features.CRM.CustomerCare.Services;
using Microsoft.EntityFrameworkCore;
using MediatR;

namespace HRM.Application.Features.CRM.CustomerCare.Queries.GetCustomerInteractionById;

internal sealed class GetCustomerInteractionByIdQueryHandler
    : IRequestHandler<GetCustomerInteractionByIdQuery, OperationResult<CustomerInteractionDto>>
{
    private readonly ICRMReadDbContext _dbContext;
    private readonly CustomerCrmAccessService _accessService;

    public GetCustomerInteractionByIdQueryHandler(
        ICRMReadDbContext dbContext,
        CustomerCrmAccessService accessService)
    {
        _dbContext = dbContext;
        _accessService = accessService;
    }

    public async Task<OperationResult<CustomerInteractionDto>> Handle(
        GetCustomerInteractionByIdQuery request,
        CancellationToken cancellationToken)
    {
        var scope = await _accessService.BuildScopeAsync(cancellationToken);
        var visibleCustomerIds = _accessService.VisibleCustomers(scope).Select(x => x.CustomerId);

        var dto = await ProjectInteractions()
            .FirstOrDefaultAsync(x =>
                x.InteractionId == request.InteractionId &&
                visibleCustomerIds.Contains(x.CustomerId),
                cancellationToken);

        return dto is null
            ? OperationResult<CustomerInteractionDto>.Fail("Interaction was not found.")
            : OperationResult<CustomerInteractionDto>.Ok(dto);
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
