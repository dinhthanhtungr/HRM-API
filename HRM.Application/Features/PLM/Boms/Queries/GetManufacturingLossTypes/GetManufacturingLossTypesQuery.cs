using HRM.Application.Features.PLM.Boms.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.Boms.Queries.GetManufacturingLossTypes;

public sealed record GetManufacturingLossTypesQuery(bool IncludeInactive = false)
    : IRequest<IReadOnlyList<ManufacturingLossTypeDto>>;
