using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Domain.Enums.Boms;
using MediatR;

namespace HRM.Application.Features.PLM.Boms.Queries.GetBomDefinition;

public sealed record GetBomDefinitionQuery(Guid BomDefinitionId, BomType? ExpectedBomType = null)
    : IRequest<BomDefinitionDetailDto?>;
