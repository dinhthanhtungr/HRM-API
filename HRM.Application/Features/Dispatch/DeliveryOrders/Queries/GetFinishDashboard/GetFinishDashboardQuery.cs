using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.Dispatch.DeliveryOrders.Dtos.Reports;
using MediatR;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Queries.GetFinishDashboard;

public sealed record GetFinishDashboardQuery : IRequest<OperationResult<FinishDashboardDto>>
{
    public DateTime From { get; init; }
    public DateTime To { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 15;
    public bool OwnOnly { get; init; }
    public int NormalizedPage => Math.Max(1, PageNumber);
    public int NormalizedSize => Math.Clamp(PageSize, 1, 100);
}

