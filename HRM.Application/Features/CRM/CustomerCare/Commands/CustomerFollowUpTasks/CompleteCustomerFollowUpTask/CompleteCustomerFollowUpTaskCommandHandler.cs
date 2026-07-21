using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Commands.CustomerFollowUpTasks;
using HRM.Domain.Enums.WorkTaskEnums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.CustomerCare.Commands.CustomerFollowUpTasks.CompleteCustomerFollowUpTask;

internal sealed class CompleteCustomerFollowUpTaskCommandHandler
    : IRequestHandler<CompleteCustomerFollowUpTaskCommand, OperationResult>
{
    private readonly CustomerFollowUpTaskCommandSupport _support;

    public CompleteCustomerFollowUpTaskCommandHandler(CustomerFollowUpTaskCommandSupport support)
    {
        _support = support;
    }

    /// <summary>
    /// Hoàn tất hoặc hủy WorkTask, lưu người thực hiện và tính lại ngày follow-up gần nhất của customer.
    /// </summary>
    public async Task<OperationResult> Handle(CompleteCustomerFollowUpTaskCommand request, CancellationToken cancellationToken)
    {
        var body = request.Request;
        if (body.Status is not (WorkTaskStatus.Done or WorkTaskStatus.Canceled))
        {
            return OperationResult.Fail("Completion status must be Done or Canceled.");
        }

        var scope = await _support.AccessService.BuildScopeAsync(cancellationToken);
        var task = await _support.GetVisibleTaskForUpdateAsync(request.TaskId, scope, cancellationToken);
        if (task is null)
        {
            return OperationResult.Fail("Task was not found or is outside your scope.");
        }

        var now = _support.DateTimeProvider.Now;
        var completionNote = CustomerFollowUpTaskCommandSupport.Normalize(body.CompletionNote);
        var affected = await _support.WriteDbContext.WorkTasks
            .Where(x => x.Id == request.TaskId && x.CompanyId == scope.CompanyId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, body.Status)
                .SetProperty(x => x.CompletedDate, now)
                .SetProperty(x => x.CompletedBy, scope.EmployeeId)
                .SetProperty(x => x.CompletionNote, completionNote)
                .SetProperty(x => x.UpdatedDate, now)
                .SetProperty(x => x.UpdatedBy, scope.EmployeeId),
                cancellationToken);

        if (affected == 0)
        {
            return OperationResult.Fail("Task was not updated.");
        }

        var customer = await _support.GetTaskCustomerForUpdateAsync(task.Id, scope.CompanyId, cancellationToken);
        if (customer is not null)
        {
            customer.NextFollowUpDate = await _support.ResolveNextFollowUpAsync(customer.CustomerId, scope.CompanyId, task.Id, null, cancellationToken);
            await _support.WriteDbContext.SaveChangesAsync(cancellationToken);
        }

        return OperationResult.Ok();
    }
}
