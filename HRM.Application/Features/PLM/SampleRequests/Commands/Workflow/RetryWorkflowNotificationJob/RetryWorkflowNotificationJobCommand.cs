using HRM.Application.Features.PLM.SampleRequests.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.RetryWorkflowNotificationJob;

public sealed record RetryWorkflowNotificationJobCommand(long JobId) : IRequest<RetryWorkflowNotificationJobResult>;
