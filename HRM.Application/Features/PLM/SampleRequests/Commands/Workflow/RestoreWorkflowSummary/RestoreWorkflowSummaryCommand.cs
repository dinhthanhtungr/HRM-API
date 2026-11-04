using HRM.Application.Features.PLM.SampleRequests.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.RestoreWorkflowSummary;

public sealed record RestoreWorkflowSummaryCommand(long JobId) : IRequest<RetryWorkflowNotificationJobResult>;
