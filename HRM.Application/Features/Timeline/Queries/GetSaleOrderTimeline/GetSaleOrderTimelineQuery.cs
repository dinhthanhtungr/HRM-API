using HRM.Application.Commons.Pagination;
using HRM.Application.Features.Timeline.Dtos;
using HRM.Domain.Enums.Logs;
using HRM.Domain.Enums.Orders;
using MediatR;

namespace HRM.Application.Features.Timeline.Queries.GetSaleOrderTimeline;

/// <summary>
/// Lấy timeline tổng quan SaleOrder có phân trang theo phạm vi khách hàng mà người dùng hiện tại được xem.
/// Hỗ trợ lọc theo trạng thái, người tạo log, công ty, loại sự kiện và mốc thời gian nghiệp vụ.
/// </summary>
public sealed class GetSaleOrderTimelineQuery : PaginationQuery, IRequest<PagedResult<SaleOrderTimelineCardDto>>
{
    public Guid? Id { get; init; }
    public Guid? CreatedBy { get; init; }
    public Guid? CompanyId { get; init; }
    public EventType? EventType { get; init; }
    public string? Status { get; init; }
    public DateTime? FromCreated { get; init; }
    public DateTime? ToCreated { get; init; }
    public TimelineCreatedScope CreatedScope { get; init; } = TimelineCreatedScope.Merchandise;
    public bool? HasComplaint { get; init; }
    public ComplaintReportStatus? ComplaintStatus { get; init; }
}
