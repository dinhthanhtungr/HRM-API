using HRM.Application.Commons.Pagination;
using MediatR;

namespace HRM.Application.Features.CRM.CustomerCare.Queries.Tracking;

public sealed record ListCustomerTrackingInteractionsQuery(Guid CustomerId, int PageNumber)
    : IRequest<PagedResult<CustomerTrackingInteractionDto>?>;
