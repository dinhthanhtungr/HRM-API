using HRM.Application.Features.PLM.Boms.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.Boms.Queries.GetFormulaDrivenManufacturingBomQueue;

public sealed record GetFormulaDrivenManufacturingBomQueueQuery
    : IRequest<IReadOnlyList<FormulaDrivenManufacturingBomQueueItemDto>>;
