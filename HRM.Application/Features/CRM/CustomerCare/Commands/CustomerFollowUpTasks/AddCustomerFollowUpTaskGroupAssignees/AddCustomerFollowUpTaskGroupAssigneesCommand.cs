using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using MediatR;

namespace HRM.Application.Features.CRM.CustomerCare.Commands.CustomerFollowUpTasks.AddCustomerFollowUpTaskGroupAssignees;

/// <summary>
/// Gán các thành viên hợp lệ của một group vào follow-up task.
/// </summary>
public sealed class AddCustomerFollowUpTaskGroupAssigneesCommand
    : IRequest<OperationResult<IReadOnlyList<CustomerFollowUpTaskAssigneeDto>>>
{
    public Guid TaskId { get; init; }
    public AddCustomerFollowUpTaskGroupAssigneesRequest Request { get; init; } = new();
}
