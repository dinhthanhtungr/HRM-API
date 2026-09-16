using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Boms.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.Boms.Commands.InitializeProductionOrderLosses;

public sealed record InitializeProductionOrderLossesCommand(Guid MfgProductionOrderId)
    : IRequest<OperationResult<IReadOnlyList<ProductionOrderLossDto>>>;
