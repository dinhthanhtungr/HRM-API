using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.PLM.Manufacturing.Commands.FinishProductionOrder;

public sealed record FinishProductionOrderCommand(Guid Id) : IRequest<OperationResult>;
