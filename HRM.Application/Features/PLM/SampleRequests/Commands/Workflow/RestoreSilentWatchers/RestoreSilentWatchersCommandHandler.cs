using HRM.Application.Features.PLM.SampleRequests.Dtos;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.RestoreSilentWatchers;

internal sealed class RestoreSilentWatchersCommandHandler(SilentWatcherReconciliationService service)
    : IRequestHandler<RestoreSilentWatchersCommand, SilentWatcherReconciliationDto?>
{
    public Task<SilentWatcherReconciliationDto?> Handle(RestoreSilentWatchersCommand request, CancellationToken ct)
        => service.ExecuteAsync(request.JobId, request.Fingerprint, true, ct);
}
