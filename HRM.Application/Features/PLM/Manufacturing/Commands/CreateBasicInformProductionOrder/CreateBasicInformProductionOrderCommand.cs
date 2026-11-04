using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.ProductionOrders.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.Manufacturing.Commands.CreateBasicInformProductionOrder;

public sealed record CreateBasicInformProductionOrderCommand(CreateBasicInformProductionOrderRequest Request) : IRequest<OperationResult<CreateProductionOrderInformResult>>;
