using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Commands.CustomerFollowUpTasks;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.CustomerCare.Commands.CustomerFollowUpTasks.RemoveCustomerFollowUpTaskAssignee;

internal sealed class RemoveCustomerFollowUpTaskAssigneeCommandHandler
    : IRequestHandler<RemoveCustomerFollowUpTaskAssigneeCommand, OperationResult>
{
    private readonly CustomerFollowUpTaskCommandSupport _support;

    public RemoveCustomerFollowUpTaskAssigneeCommandHandler(CustomerFollowUpTaskCommandSupport support)
    {
        _support = support;
    }

    /// <summary>
    /// Soft-remove một task assignee; không xóa dòng để giữ audit giao việc.
    /// </summary>
    public async Task<OperationResult> Handle(
        RemoveCustomerFollowUpTaskAssigneeCommand request,
        CancellationToken cancellationToken)
    {
        var scope = await _support.AccessService.BuildScopeAsync(cancellationToken);
        var task = await _support.GetVisibleTaskForUpdateAsync(request.TaskId, scope, cancellationToken);
        if (task is null)
        {
            return OperationResult.Fail("Task was not found.");
        }

        var assignee = await _support.WriteDbContext.WorkTaskAssignees.FirstOrDefaultAsync(x =>
            x.WorkTaskId == request.TaskId && x.EmployeeId == request.EmployeeId && x.IsActive, cancellationToken);
        if (assignee is null)
        {
            return OperationResult.Fail("Assignee was not found.");
        }

        assignee.IsActive = false;
        assignee.IsPrimary = false;
        if (task.AssignedToEmployeeId == request.EmployeeId)
        {
            task.AssignedToEmployeeId = null;
        }

        task.UpdatedDate = _support.DateTimeProvider.Now;
        task.UpdatedBy = scope.EmployeeId;
        await _support.WriteDbContext.SaveChangesAsync(cancellationToken);
        return OperationResult.Ok();
    }
}
