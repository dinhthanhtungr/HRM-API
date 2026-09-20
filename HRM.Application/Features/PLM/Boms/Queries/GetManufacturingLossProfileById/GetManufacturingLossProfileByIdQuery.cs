using HRM.Application.Features.PLM.Boms.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.Boms.Queries.GetManufacturingLossProfileById;

public sealed record GetManufacturingLossProfileByIdQuery(Guid ProfileId)
    : IRequest<ManufacturingLossProfileDto?>;
