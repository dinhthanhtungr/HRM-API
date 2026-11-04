using HRM.Application.Features.PLM.SampleRequests.Dtos;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.PreviewSilentWatchers;

internal sealed class PreviewSilentWatchersQueryHandler(SilentWatcherReconciliationService service)
    : IRequestHandler<PreviewSilentWatchersQuery, SilentWatcherReconciliationDto?>
{
    public Task<SilentWatcherReconciliationDto?> Handle(PreviewSilentWatchersQuery request, CancellationToken ct)
        => service.ExecuteAsync(request.JobId, null, false, ct);
}
