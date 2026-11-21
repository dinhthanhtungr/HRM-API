using HRM.Application.Commons.Pagination;
using MediatR;

namespace HRM.Application.Features.CRM.CustomerCare.Queries.Tracking;

public sealed record ListCustomerTrackingQuery(string? Keyword, int PageNumber)
    : IRequest<PagedResult<CustomerTrackingDto>?>;
