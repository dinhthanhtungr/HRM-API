using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Boms.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.Boms.Queries.ExplodeBom;

public sealed record ExplodeBomQuery(Guid BomVersionId, decimal OutputQuantity, int MaxDepth = 12)
    : IRequest<OperationResult<BomExplosionDto>>;
