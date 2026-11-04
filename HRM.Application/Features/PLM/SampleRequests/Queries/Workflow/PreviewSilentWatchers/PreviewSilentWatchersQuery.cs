using HRM.Application.Features.PLM.SampleRequests.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.PreviewSilentWatchers;

public sealed record PreviewSilentWatchersQuery(long JobId) : IRequest<SilentWatcherReconciliationDto?>;
