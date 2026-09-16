using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Boms.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.Boms.Commands.PatchProductionOrderLoss;

public sealed record PatchProductionOrderLossCommand(
    Guid MfgProductionOrderId,
    Guid LossId,
    PatchProductionOrderLossRequest Request)
    : IRequest<OperationResult<ProductionOrderLossDto>>;
