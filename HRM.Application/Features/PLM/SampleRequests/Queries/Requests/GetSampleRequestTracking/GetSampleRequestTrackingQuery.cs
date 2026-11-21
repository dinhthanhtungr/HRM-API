using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestTracking;

public sealed record GetSampleRequestTrackingQuery(Guid SampleRequestId) : IRequest<SampleRequestTrackingDto?>;
