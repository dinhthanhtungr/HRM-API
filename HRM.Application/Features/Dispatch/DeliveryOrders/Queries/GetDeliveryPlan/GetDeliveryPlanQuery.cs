using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.Dispatch.DeliveryOrders.Dtos;
using MediatR;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Queries.GetDeliveryPlan;

public sealed class GetDeliveryPlanQuery : PaginationQuery, IRequest<OperationResult<DeliveryPlanResponseDto>>
{
    public DateOnly? Date { get; init; }
    public bool IncludeOverdue { get; init; } = true;
    /// <summary>Bỏ giới hạn ngày kế hoạch, gồm cả đơn chưa có ngày hứa giao.</summary>
    public bool IncludeAllOrders { get; init; }
    public Guid? CustomerId { get; init; }
    public DeliveryPlanProgress? Progress { get; init; }
}
