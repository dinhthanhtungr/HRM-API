using HRM.Application.Commons.Pagination;
using HRM.Application.Features.Timeline.Dtos;
using HRM.Domain.Enums.Logs;
using MediatR;

namespace HRM.Application.Features.Timeline.Queries.GetSaleOrderTimelineDetail;

/// <summary>
/// Lấy timeline chi tiết theo từng dòng hàng của một SaleOrder mà người dùng hiện tại được phép xem.
/// </summary>
public sealed class GetSaleOrderTimelineDetailQuery : PaginationQuery, IRequest<PagedResult<SaleOrderTimelineDetailRowDto>>
{
    public Guid Id { get; init; }
    public string? Status { get; init; }
}
