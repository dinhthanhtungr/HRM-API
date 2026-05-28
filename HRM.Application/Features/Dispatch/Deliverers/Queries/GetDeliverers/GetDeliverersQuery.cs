using HRM.Application.Commons.Pagination;
using HRM.Application.Features.Dispatch.Deliverers.Dtos;
using MediatR;

namespace HRM.Application.Features.Dispatch.Deliverers.Queries.GetDeliverers;

public sealed class GetDeliverersQuery
    : PaginationQuery, IRequest<PagedResult<DelivererDto>>
{
    public bool? IsActive { get; init; } = true;
}
