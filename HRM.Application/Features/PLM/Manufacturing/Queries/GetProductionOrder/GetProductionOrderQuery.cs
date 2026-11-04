using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;
using MediatR;

namespace HRM.Application.Features.PLM.Manufacturing.Queries.GetProductionOrder;

public sealed record GetProductionOrderQuery(Guid Id) : IRequest<OperationResult<GetMfgProductionOrder>>;
