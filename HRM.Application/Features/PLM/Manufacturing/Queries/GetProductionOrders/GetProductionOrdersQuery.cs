using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;
using MediatR;

namespace HRM.Application.Features.PLM.Manufacturing.Queries.GetProductionOrders;

public sealed record GetProductionOrdersQuery : IRequest<OperationResult<PagedResult<GetSummaryMfgProductionOrder>>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 15;
    public string? Keyword { get; init; }
    public Guid? MfgProductionOrderId { get; init; }
    public List<string> Statuses { get; init; } = [];
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public bool IsLastUpdate { get; init; }
}
