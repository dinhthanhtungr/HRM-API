using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.PLM.Manufacturing.Commands.RollbackProductionOrderStock;

public sealed record RollbackProductionOrderStockCommand(Guid Id, string Revision) : IRequest<OperationResult>;
