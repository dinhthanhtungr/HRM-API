using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Commands.CustomerFollowUpTasks;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.CustomerCare.Commands.CustomerFollowUpTasks.AddCustomerFollowUpTaskGroupAssignees;

internal sealed class AddCustomerFollowUpTaskGroupAssigneesCommandHandler
    : IRequestHandler<AddCustomerFollowUpTaskGroupAssigneesCommand, OperationResult<IReadOnlyList<CustomerFollowUpTaskAssigneeDto>>>
{
    private readonly CustomerFollowUpTaskCommandSupport _support;

    public AddCustomerFollowUpTaskGroupAssigneesCommandHandler(CustomerFollowUpTaskCommandSupport support)
    {
        _support = support;
    }

    /// <summary>
    /// Gán các employee active của một group mà current user có quyền quản lý vào WorkTask.
    /// </summary>
    public async Task<OperationResult<IReadOnlyList<CustomerFollowUpTaskAssigneeDto>>> Handle(
        AddCustomerFollowUpTaskGroupAssigneesCommand request,
        CancellationToken cancellationToken)
    {
        var scope = await _support.AccessService.BuildScopeAsync(cancellationToken);
        var body = request.Request;
        if (body.GroupId == Guid.Empty ||
            !scope.HasFullCustomerView && !scope.LeaderGroupIds.Contains(body.GroupId))
        {
            return OperationResult<IReadOnlyList<CustomerFollowUpTaskAssigneeDto>>.Fail("Group is outside your scope.");
        }

        var task = await _support.GetVisibleTaskForUpdateAsync(request.TaskId, scope, cancellationToken);
        if (task is null)
        {
            return OperationResult<IReadOnlyList<CustomerFollowUpTaskAssigneeDto>>.Fail("Task was not found.");
        }

        var employeeIds = await _support.ReadDbContext.MemberInGroups.AsNoTracking()
            .Where(x => x.GroupId == body.GroupId && x.IsActive && x.Profile.HasValue &&
                        x.ProfileNavigation != null && x.ProfileNavigation.CompanyId == scope.CompanyId && x.ProfileNavigation.IsActive)
            .Select(x => x.Profile!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (employeeIds.Count == 0)
        {
            return OperationResult<IReadOnlyList<CustomerFollowUpTaskAssigneeDto>>.Fail("Group has no active employees.");
        }

        if (body.PrimaryEmployeeId.HasValue && !employeeIds.Contains(body.PrimaryEmployeeId.Value))
        {
            return OperationResult<IReadOnlyList<CustomerFollowUpTaskAssigneeDto>>.Fail("Primary employee is not in this group.");
        }

        foreach (var employeeId in employeeIds)
        {
            await _support.UpsertTaskAssigneeAsync(task, employeeId, employeeId == body.PrimaryEmployeeId, scope.EmployeeId, cancellationToken);
        }

        await _support.WriteDbContext.SaveChangesAsync(cancellationToken);
        foreach (var employeeId in employeeIds.Where(x => x != scope.EmployeeId))
        {
            await _support.PublishAssigneeNotificationAsync(task, employeeId, scope, cancellationToken);
        }

        return OperationResult<IReadOnlyList<CustomerFollowUpTaskAssigneeDto>>.Ok(
            await _support.ProjectAssignees(task.Id).ToListAsync(cancellationToken));
    }
}
