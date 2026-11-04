using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.PLM.Manufacturing.Commands.CheckProductionOrder;

public sealed record CheckProductionOrderCommand(Guid Id, bool? IsPrintedStock) : IRequest<OperationResult>;
