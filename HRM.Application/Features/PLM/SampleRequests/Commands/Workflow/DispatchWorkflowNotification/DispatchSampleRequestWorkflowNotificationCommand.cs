using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.DispatchWorkflowNotification;

// Chỉ worker nội bộ gọi; không expose dưới dạng API nhận outbox ID từ client.
public sealed record DispatchSampleRequestWorkflowNotificationCommand(long OutboxId) : IRequest<OperationResult<Guid>>;
