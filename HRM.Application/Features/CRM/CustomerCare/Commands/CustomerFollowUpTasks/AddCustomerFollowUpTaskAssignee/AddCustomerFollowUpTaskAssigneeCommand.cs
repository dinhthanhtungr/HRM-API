using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using MediatR;

namespace HRM.Application.Features.CRM.CustomerCare.Commands.CustomerFollowUpTasks.AddCustomerFollowUpTaskAssignee;

/// <summary>
/// Gán thêm một nhân viên vào follow-up task sau khi kiểm tra visibility và employee scope.
/// </summary>
public sealed class AddCustomerFollowUpTaskAssigneeCommand
    : IRequest<OperationResult<CustomerFollowUpTaskAssigneeDto>>
{
    public Guid TaskId { get; init; }
    public AddCustomerFollowUpTaskAssigneeRequest Request { get; init; } = new();
}
