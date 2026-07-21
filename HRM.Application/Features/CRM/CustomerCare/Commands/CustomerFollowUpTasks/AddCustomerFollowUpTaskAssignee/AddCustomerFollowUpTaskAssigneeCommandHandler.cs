using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Commands.CustomerFollowUpTasks;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.CustomerCare.Commands.CustomerFollowUpTasks.AddCustomerFollowUpTaskAssignee;

internal sealed class AddCustomerFollowUpTaskAssigneeCommandHandler
    : IRequestHandler<AddCustomerFollowUpTaskAssigneeCommand, OperationResult<CustomerFollowUpTaskAssigneeDto>>
{
    private readonly CustomerFollowUpTaskCommandSupport _support;

    public AddCustomerFollowUpTaskAssigneeCommandHandler(CustomerFollowUpTaskCommandSupport support)
    {
        _support = support;
    }

    /// <summary>
    /// Thêm/reactivate assignee cho task; primary mới thay primary cũ và phát Notification cho người được giao.
    /// </summary>
    public async Task<OperationResult<CustomerFollowUpTaskAssigneeDto>> Handle(
        AddCustomerFollowUpTaskAssigneeCommand request,
        CancellationToken cancellationToken)
    {
        var body = request.Request;
        if (body.EmployeeId == Guid.Empty)
        {
            return OperationResult<CustomerFollowUpTaskAssigneeDto>.Fail("Employee id is required.");
        }

        var scope = await _support.AccessService.BuildScopeAsync(cancellationToken);
        var task = await _support.GetVisibleTaskForUpdateAsync(request.TaskId, scope, cancellationToken);
        if (task is null)
        {
            return OperationResult<CustomerFollowUpTaskAssigneeDto>.Fail("Task was not found.");
        }

        var employee = await _support.AccessService.ResolveEmployeeAsync(scope, body.EmployeeId, false, cancellationToken);
        if (!employee.IsAllowed || !employee.EmployeeId.HasValue)
        {
            return OperationResult<CustomerFollowUpTaskAssigneeDto>.Fail("Employee is outside your scope.");
        }

        var assignee = await _support.UpsertTaskAssigneeAsync(task, employee.EmployeeId.Value, body.IsPrimary, scope.EmployeeId, cancellationToken);
        await _support.WriteDbContext.SaveChangesAsync(cancellationToken);
        await _support.PublishAssigneeNotificationAsync(task, employee.EmployeeId.Value, scope, cancellationToken);

        var dto = await _support.ProjectAssignees(task.Id).FirstAsync(x => x.AssigneeId == assignee.Id, cancellationToken);
        return OperationResult<CustomerFollowUpTaskAssigneeDto>.Ok(dto);
    }
}
