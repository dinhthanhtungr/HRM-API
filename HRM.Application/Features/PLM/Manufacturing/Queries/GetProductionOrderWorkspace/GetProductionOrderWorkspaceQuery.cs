using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;
using MediatR;

namespace HRM.Application.Features.PLM.Manufacturing.Queries.GetProductionOrderWorkspace;

public sealed record GetProductionOrderWorkspaceQuery(Guid Id) : IRequest<OperationResult<ProductionOrderWorkspaceDto>>;
