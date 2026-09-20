using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Domain.Enums.Boms;
using MediatR;

namespace HRM.Application.Features.PLM.Boms.Queries.GetManufacturingLossProfiles;

public sealed record GetManufacturingLossProfilesQuery(ManufacturingLossProfileStatus? Status)
    : IRequest<IReadOnlyList<ManufacturingLossProfileSummaryDto>>;
