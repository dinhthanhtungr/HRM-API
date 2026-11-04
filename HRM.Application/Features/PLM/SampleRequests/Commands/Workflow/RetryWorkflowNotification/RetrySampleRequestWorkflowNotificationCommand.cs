using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.RetryWorkflowNotification;

public sealed record RetrySampleRequestWorkflowNotificationCommand(Guid SampleRequestId, Guid EventId) : IRequest<OperationResult<Guid>>;
