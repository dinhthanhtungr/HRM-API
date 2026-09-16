using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.PLM.Boms.Commands.FinalizeProductionOrderLosses;

public sealed record FinalizeProductionOrderLossesCommand(Guid MfgProductionOrderId)
    : IRequest<OperationResult>;
