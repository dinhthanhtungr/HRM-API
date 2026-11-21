using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.RetryWorkflowNotificationJob;

public sealed record RecoverReadyWorkflowNotificationsCommand(string? SampleRequestCode = null)
    : IRequest<RecoverReadyWorkflowNotificationsResult?>;

public sealed record RecoverReadyWorkflowNotificationsResult(int Reviewed, int Queued, int Conflicts);
