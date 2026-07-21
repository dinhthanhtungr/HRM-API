using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Patching;
using HRM.Application.Features.CRM.CustomerCare.Services;
using HRM.Domain.Entities.WorkTaskSchema;
using HRM.Domain.Enums.WorkTaskEnums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.CustomerCare.Commands.UpdateCustomerWorkPlan;

internal sealed class UpdateCustomerWorkPlanCommandHandler
    : IRequestHandler<UpdateCustomerWorkPlanCommand, OperationResult>
{
    private readonly ICRMWriteDbContext _writeDbContext;
    private readonly CustomerCrmAccessService _accessService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public UpdateCustomerWorkPlanCommandHandler(
        ICRMWriteDbContext writeDbContext,
        CustomerCrmAccessService accessService,
        IDateTimeProvider dateTimeProvider)
    {
        _writeDbContext = writeDbContext;
        _accessService = accessService;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <summary>
    /// Cập nhật WorkPlan và đồng bộ primary WorkPlanAssignee khi đổi người phụ trách.
    /// </summary>
    public async Task<OperationResult> Handle(UpdateCustomerWorkPlanCommand request, CancellationToken cancellationToken)
    {
        var scope = await _accessService.BuildScopeAsync(cancellationToken);
        var visibleCustomerIds = _accessService.VisibleCustomers(scope).Select(x => x.CustomerId);

        var isVisible = await _writeDbContext.WorkPlans
            .AsNoTracking()
            .AnyAsync(x =>
                x.Id == request.PlanId &&
                x.CompanyId == scope.CompanyId &&
                x.References.Any(r => r.ReferenceType == WorkReferenceType.Customer && visibleCustomerIds.Contains(r.ReferenceId)),
                cancellationToken);

        if (!isVisible)
        {
            return OperationResult.Fail("Work plan was not found.");
        }

        var plan = _writeDbContext.WorkPlans.Local.FirstOrDefault(x => x.Id == request.PlanId)
                   ?? await _writeDbContext.WorkPlans.AsTracking().FirstOrDefaultAsync(x =>
            x.Id == request.PlanId &&
            x.CompanyId == scope.CompanyId, cancellationToken);

        if (plan is null)
        {
            return OperationResult.Fail("Work plan was not found.");
        }

        var body = request.Request;
        if (body.PlanName is { } rawName)
        {
            var name = Normalize(rawName);
            if (name is null || name.Length > CustomerCrmTaskRules.MaxTitleLength)
            {
                return OperationResult.Fail("Plan name is invalid.");
            }

            PatchHelper.SetTrimmed(rawName, () => plan.PlanName, value => plan.PlanName = value ?? string.Empty);
        }

        if (body.Status.HasValue && !Enum.IsDefined(body.Status.Value) ||
            body.Priority.HasValue && !Enum.IsDefined(body.Priority.Value))
        {
            return OperationResult.Fail("Plan status or priority is invalid.");
        }

        var start = body.StartDate ?? plan.StartDate;
        var end = body.EndDate ?? plan.EndDate;
        if (start.HasValue && end.HasValue && end < start)
        {
            return OperationResult.Fail("Plan end date cannot be before start date.");
        }

        if (body.AssignedSaleEmployeeId.HasValue)
        {
            var employee = await _accessService.ResolveEmployeeAsync(scope, body.AssignedSaleEmployeeId, false, cancellationToken);
            if (!employee.IsAllowed)
            {
                return OperationResult.Fail("Assigned sale employee is outside your scope.");
            }

            plan.AssignedToEmployeeId = employee.EmployeeId;
            if (employee.EmployeeId.HasValue)
            {
                await EnsurePrimaryPlanAssigneeAsync(plan.Id, employee.EmployeeId.Value, scope.EmployeeId, cancellationToken);
            }
        }

        PatchHelper.SetTrimmed(body.Objective, () => plan.Objective, value => plan.Objective = value);
        PatchHelper.SetTrimmed(body.Strategy, () => plan.Strategy, value => plan.Strategy = value);
        PatchHelper.SetTrimmed(body.DiscussionSummary, () => plan.DiscussionSummary, value => plan.DiscussionSummary = value);
        PatchHelper.SetTrimmed(body.NextAction, () => plan.NextAction, value => plan.NextAction = value);
        PatchHelper.SetIfHasValue(body.Status, () => plan.Status, value => plan.Status = value);
        PatchHelper.SetIfHasValue(body.Priority, () => plan.Priority, value => plan.Priority = value);
        if (body.StartDate.HasValue)
        {
            PatchHelper.SetNullable(body.StartDate, () => plan.StartDate, value => plan.StartDate = value);
        }

        if (body.EndDate.HasValue)
        {
            PatchHelper.SetNullable(body.EndDate, () => plan.EndDate, value => plan.EndDate = value);
        }

        if (body.NextFollowUpDate.HasValue)
        {
            PatchHelper.SetNullable(body.NextFollowUpDate, () => plan.NextFollowUpDate, value => plan.NextFollowUpDate = value);
        }

        PatchHelper.SetIfHasValue(body.IsActive, () => plan.IsActive, value => plan.IsActive = value);
        plan.UpdatedDate = _dateTimeProvider.Now;
        plan.UpdatedBy = scope.EmployeeId;

        await _writeDbContext.SaveChangesAsync(cancellationToken);
        return OperationResult.Ok();
    }

    private async Task EnsurePrimaryPlanAssigneeAsync(Guid planId, Guid employeeId, Guid actorId, CancellationToken cancellationToken)
    {
        var currentPrimary = await _writeDbContext.WorkPlanAssignees
            .Where(x => x.WorkPlanId == planId && x.IsActive && x.IsPrimary)
            .ToListAsync(cancellationToken);
        foreach (var item in currentPrimary)
        {
            item.IsPrimary = false;
        }

        var assignee = await _writeDbContext.WorkPlanAssignees
            .OrderByDescending(x => x.IsActive)
            .FirstOrDefaultAsync(x => x.WorkPlanId == planId && x.EmployeeId == employeeId, cancellationToken);

        if (assignee is null)
        {
            await _writeDbContext.WorkPlanAssignees.AddAsync(new WorkPlanAssignee
            {
                Id = Guid.CreateVersion7(),
                WorkPlanId = planId,
                EmployeeId = employeeId,
                IsPrimary = true,
                IsActive = true,
                CreatedDate = _dateTimeProvider.Now,
                CreatedBy = actorId
            }, cancellationToken);
        }
        else
        {
            assignee.IsActive = true;
            assignee.IsPrimary = true;
        }
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
