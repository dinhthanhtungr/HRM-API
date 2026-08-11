using HRM.Application.Commons.Pagination;
using HRM.Application.Features.Timeline.Dtos;
using HRM.Domain.Enums.Logs;
using MediatR;

namespace HRM.Application.Features.Timeline.Queries.GetTimelineBySource;

/// <summary>
/// Lấy EventLog active có phân trang của một SourceId, hỗ trợ lọc theo loại sự kiện, trạng thái,
/// người tạo và khoảng ngày tạo.
/// </summary>
public sealed class GetTimelineBySourceQuery : PaginationQuery, IRequest<PagedResult<TimelineItemDto>>
{
    public Guid SourceId { get; init; }
    public EventType? EventType { get; init; }
    public string? Status { get; init; }
    public Guid? CreatedBy { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
}
