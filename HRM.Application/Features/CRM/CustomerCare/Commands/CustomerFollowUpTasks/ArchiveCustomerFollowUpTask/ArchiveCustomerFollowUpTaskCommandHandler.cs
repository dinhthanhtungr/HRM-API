using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Commands.CustomerFollowUpTasks;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.CustomerCare.Commands.CustomerFollowUpTasks.ArchiveCustomerFollowUpTask;

internal sealed class ArchiveCustomerFollowUpTaskCommandHandler
    : IRequestHandler<ArchiveCustomerFollowUpTaskCommand, OperationResult>
{
    private readonly ICRMWriteDbContext _writeDbContext;
    private readonly CustomerFollowUpTaskCommandSupport _support;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ArchiveCustomerFollowUpTaskCommandHandler(
        ICRMWriteDbContext writeDbContext,
        CustomerFollowUpTaskCommandSupport support,
        IDateTimeProvider dateTimeProvider)
    {
        _writeDbContext = writeDbContext;
        _support = support;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <summary>
    /// Archive mềm WorkTask và tính lại Customer.NextFollowUpDate.
    /// </summary>
    public async Task<OperationResult> Handle(ArchiveCustomerFollowUpTaskCommand request, CancellationToken cancellationToken)
    {
        var scope = await _support.AccessService.BuildScopeAsync(cancellationToken);
        var task = await _support.GetVisibleTaskForUpdateAsync(request.TaskId, scope, cancellationToken);
        if (task is null)
        {
            return OperationResult.Fail("Task was not found.");
        }

        var affected = await _writeDbContext.WorkTasks
            .Where(x => x.Id == request.TaskId && x.CompanyId == scope.CompanyId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.IsActive, false)
                .SetProperty(x => x.UpdatedDate, _dateTimeProvider.Now)
                .SetProperty(x => x.UpdatedBy, scope.EmployeeId),
                cancellationToken);

        var customer = await _support.GetTaskCustomerForUpdateAsync(task.Id, scope.CompanyId, cancellationToken);
        if (customer is not null)
        {
            customer.NextFollowUpDate = await _support.ResolveNextFollowUpAsync(customer.CustomerId, scope.CompanyId, task.Id, null, cancellationToken);
        }

        return affected > 0
            ? OperationResult.Ok()
            : OperationResult.Fail("Work plan was already archived or no change was saved.");
    }
}
