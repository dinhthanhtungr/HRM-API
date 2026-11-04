using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.Purchasing.MaterialPlanning.Dtos;
using MediatR;

namespace HRM.Application.Features.Purchasing.MaterialPlanning.Queries.GetPlanningLines;

public sealed class GetPlanningLinesQuery : PaginationQuery, IRequest<OperationResult<PagedResult<PlanningLineDto>>>
{
    public Guid OrderId { get; set; }
}
