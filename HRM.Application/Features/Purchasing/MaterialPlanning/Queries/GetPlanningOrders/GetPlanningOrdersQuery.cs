using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.Purchasing.MaterialPlanning.Dtos;
using MediatR;

namespace HRM.Application.Features.Purchasing.MaterialPlanning.Queries.GetPlanningOrders;

public sealed class GetPlanningOrdersQuery : PaginationQuery, IRequest<OperationResult<PagedResult<PlanningOrderDto>>>;
