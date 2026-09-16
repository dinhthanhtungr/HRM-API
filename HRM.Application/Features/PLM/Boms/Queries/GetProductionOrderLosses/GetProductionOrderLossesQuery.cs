using HRM.Application.Features.PLM.Boms.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.Boms.Queries.GetProductionOrderLosses;

public sealed record GetProductionOrderLossesQuery(Guid MfgProductionOrderId)
    : IRequest<IReadOnlyList<ProductionOrderLossDto>>;
