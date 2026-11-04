using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;
using HRM.Application.Features.PLM.ProductionOrders.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.Manufacturing.Commands.CopyProductionOrder;

public sealed record CopyProductionOrderCommand(Guid SourceId, ProductionOrderInformRequest Request) : IRequest<OperationResult<CreateProductionOrderInformResult>>;
