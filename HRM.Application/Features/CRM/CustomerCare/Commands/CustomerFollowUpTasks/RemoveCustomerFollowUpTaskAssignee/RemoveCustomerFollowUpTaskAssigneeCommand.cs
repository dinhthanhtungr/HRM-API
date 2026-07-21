using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.CRM.CustomerCare.Commands.CustomerFollowUpTasks.RemoveCustomerFollowUpTaskAssignee;

/// <summary>
/// Soft-remove một assignee khỏi follow-up task.
/// </summary>
public sealed record RemoveCustomerFollowUpTaskAssigneeCommand(Guid TaskId, Guid EmployeeId)
    : IRequest<OperationResult>;
