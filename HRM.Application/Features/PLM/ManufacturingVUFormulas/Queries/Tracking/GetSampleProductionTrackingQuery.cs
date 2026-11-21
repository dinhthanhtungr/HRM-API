using MediatR;

namespace HRM.Application.Features.PLM.ManufacturingVUFormulas.Queries.Tracking;

public sealed record GetSampleProductionTrackingQuery(Guid Id) : IRequest<SampleProductionTrackingDto?>;
