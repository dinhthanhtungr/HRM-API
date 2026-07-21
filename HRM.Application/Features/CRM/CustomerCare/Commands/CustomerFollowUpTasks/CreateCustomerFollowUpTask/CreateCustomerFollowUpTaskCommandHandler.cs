using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Commands.CustomerFollowUpTasks;
using HRM.Application.Features.CRM.CustomerCare.Services;
using HRM.Domain.Entities.WorkTaskSchema;
using HRM.Domain.Enums.WorkTaskEnums;
using MediatR;

namespace HRM.Application.Features.CRM.CustomerCare.Commands.CustomerFollowUpTasks.CreateCustomerFollowUpTask;

internal sealed class CreateCustomerFollowUpTaskCommandHandler
    : IRequestHandler<CreateCustomerFollowUpTaskCommand, OperationResult<Guid>>
{
    private readonly CustomerFollowUpTaskCommandSupport _support;

    public CreateCustomerFollowUpTaskCommandHandler(CustomerFollowUpTaskCommandSupport support)
    {
        _support = support;
    }

    /// <summary>
    /// Tạo WorkTask follow-up có Customer reference primary và primary assignee.
    /// </summary>
    public async Task<OperationResult<Guid>> Handle(CreateCustomerFollowUpTaskCommand request, CancellationToken cancellationToken)
    {
        var body = request.Request;
        var title = CustomerFollowUpTaskCommandSupport.Normalize(body.Title);

        if (body.CustomerId == Guid.Empty || title is null || title.Length > CustomerCrmTaskRules.MaxTitleLength)
        {
            return OperationResult<Guid>.Fail("Customer and task title are required.");
        }

        if (!Enum.IsDefined(body.Priority))
        {
            return OperationResult<Guid>.Fail("Task priority is invalid.");
        }

        var scope = await _support.AccessService.BuildScopeAsync(cancellationToken);
        var customer = await _support.GetVisibleCustomerForUpdateAsync(body.CustomerId, scope, cancellationToken);
        if (customer is null)
        {
            return OperationResult<Guid>.Fail("Customer was not found or is outside your scope.");
        }

        if (body.CustomerInteractionId.HasValue &&
            !await _support.InteractionBelongsToCustomerAsync(body.CustomerInteractionId.Value, customer.CustomerId, scope.CompanyId, cancellationToken))
        {
            return OperationResult<Guid>.Fail("Interaction does not belong to this customer.");
        }

        var employee = await _support.AccessService.ResolveEmployeeAsync(scope, body.AssignedSaleEmployeeId, true, cancellationToken);
        if (!employee.IsAllowed || !employee.EmployeeId.HasValue)
        {
            return OperationResult<Guid>.Fail("Assigned sale employee is outside your scope.");
        }

        var now = _support.DateTimeProvider.Now;
        var taskId = Guid.CreateVersion7();
        await _support.WriteDbContext.WorkTasks.AddAsync(new WorkTask
        {
            Id = taskId,
            Title = title,
            Description = CustomerFollowUpTaskCommandSupport.Normalize(body.Description),
            NextAction = CustomerFollowUpTaskCommandSupport.Normalize(body.NextAction),
            Status = WorkTaskStatus.Pending,
            Priority = body.Priority,
            DueDate = body.DueDate,
            AssignedToEmployeeId = employee.EmployeeId,
            CompanyId = scope.CompanyId,
            CreatedDate = now,
            CreatedBy = scope.EmployeeId,
            IsActive = true
        }, cancellationToken);

        await _support.AddTaskReferencesAsync(taskId, customer, body.CustomerInteractionId, cancellationToken);
        await _support.EnsurePrimaryTaskAssigneeAsync(taskId, employee.EmployeeId.Value, scope.EmployeeId, cancellationToken);
        customer.NextFollowUpDate = await _support.ResolveNextFollowUpAsync(customer.CustomerId, scope.CompanyId, null, body.DueDate, cancellationToken);
        await _support.WriteDbContext.SaveChangesAsync(cancellationToken);
        return OperationResult<Guid>.Ok(taskId);
    }
}
