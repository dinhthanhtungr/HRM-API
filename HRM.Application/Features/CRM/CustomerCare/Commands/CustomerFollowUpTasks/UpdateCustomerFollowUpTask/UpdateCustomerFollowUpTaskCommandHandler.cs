using HRM.Application.Commons.Models;
using HRM.Application.Commons.Patching;
using HRM.Application.Features.CRM.CustomerCare.Commands.CustomerFollowUpTasks;
using HRM.Application.Features.CRM.CustomerCare.Services;
using HRM.Domain.Enums.WorkTaskEnums;
using MediatR;

namespace HRM.Application.Features.CRM.CustomerCare.Commands.CustomerFollowUpTasks.UpdateCustomerFollowUpTask;

internal sealed class UpdateCustomerFollowUpTaskCommandHandler
    : IRequestHandler<UpdateCustomerFollowUpTaskCommand, OperationResult>
{
    private readonly CustomerFollowUpTaskCommandSupport _support;

    public UpdateCustomerFollowUpTaskCommandHandler(CustomerFollowUpTaskCommandSupport support)
    {
        _support = support;
    }

    /// <summary>
    /// Cập nhật WorkTask trong customer scope và đồng bộ Customer.NextFollowUpDate.
    /// </summary>
    public async Task<OperationResult> Handle(UpdateCustomerFollowUpTaskCommand request, CancellationToken cancellationToken)
    {
        var scope = await _support.AccessService.BuildScopeAsync(cancellationToken);
        var task = await _support.GetVisibleTaskForUpdateAsync(request.TaskId, scope, cancellationToken);
        if (task is null)
        {
            return OperationResult.Fail("Task was not found or is outside your scope.");
        }

        var body = request.Request;
        if (body.Title is { } rawTitle)
        {
            var title = CustomerFollowUpTaskCommandSupport.Normalize(rawTitle);
            if (title is null || title.Length > CustomerCrmTaskRules.MaxTitleLength)
            {
                return OperationResult.Fail("Task title is invalid.");
            }

            PatchHelper.SetTrimmed(rawTitle, () => task.Title, value => task.Title = value ?? string.Empty);
        }

        if (body.Status.HasValue && !Enum.IsDefined(body.Status.Value) ||
            body.Priority.HasValue && !Enum.IsDefined(body.Priority.Value))
        {
            return OperationResult.Fail("Task status or priority is invalid.");
        }

        if (body.AssignedSaleEmployeeId.HasValue)
        {
            var employee = await _support.AccessService.ResolveEmployeeAsync(scope, body.AssignedSaleEmployeeId, false, cancellationToken);
            if (!employee.IsAllowed || !employee.EmployeeId.HasValue)
            {
                return OperationResult.Fail("Assigned sale employee is outside your scope.");
            }

            PatchHelper.SetNullable(employee.EmployeeId, () => task.AssignedToEmployeeId, value => task.AssignedToEmployeeId = value);
            await _support.EnsurePrimaryTaskAssigneeAsync(task.Id, employee.EmployeeId.Value, scope.EmployeeId, cancellationToken);
        }

        if (body.DueDate.HasValue &&
            PatchHelper.SetNullable(body.DueDate, () => task.DueDate, value => task.DueDate = value))
        {
            task.DueReminderSentAt = null;
        }

        PatchHelper.SetTrimmed(body.Description, () => task.Description, value => task.Description = value);
        PatchHelper.SetTrimmed(body.NextAction, () => task.NextAction, value => task.NextAction = value);
        PatchHelper.SetIfHasValue(body.Status, () => task.Status, value => task.Status = value);
        PatchHelper.SetIfHasValue(body.Priority, () => task.Priority, value => task.Priority = value);
        PatchHelper.SetIfHasValue(body.IsActive, () => task.IsActive, value => task.IsActive = value);
        task.UpdatedDate = _support.DateTimeProvider.Now;
        task.UpdatedBy = scope.EmployeeId;

        if (CustomerCrmTaskRules.IsTerminal(task.Status))
        {
            task.CompletedDate ??= _support.DateTimeProvider.Now;
            task.CompletedBy ??= scope.EmployeeId;
        }
        else
        {
            task.CompletedDate = null;
            task.CompletedBy = null;
        }

        var customer = await _support.GetTaskCustomerForUpdateAsync(task.Id, scope.CompanyId, cancellationToken);
        if (customer is not null)
        {
            customer.NextFollowUpDate = await _support.ResolveNextFollowUpAsync(
                customer.CustomerId,
                scope.CompanyId,
                task.Id,
                task.IsActive && CustomerCrmTaskRules.IsOpen(task.Status) ? task.DueDate : null,
                cancellationToken);
        }

        var db = (Microsoft.EntityFrameworkCore.DbContext)_support.WriteDbContext;
        var state = db.Entry(task).State;

        await _support.WriteDbContext.SaveChangesAsync(cancellationToken);
        return OperationResult.Ok();
    }
}
