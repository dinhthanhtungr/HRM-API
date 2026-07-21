using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using HRM.Application.Features.CRM.CustomerCare.Services;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.WorkTaskSchema;
using HRM.Domain.Enums.WorkTaskEnums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.CustomerCare.Commands.CreateCustomerWorkPlan;

internal sealed class CreateCustomerWorkPlanCommandHandler
    : IRequestHandler<CreateCustomerWorkPlanCommand, OperationResult<Guid>>
{
    private readonly ICRMWriteDbContext _writeDbContext;
    private readonly CustomerCrmAccessService _accessService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public CreateCustomerWorkPlanCommandHandler(
        ICRMWriteDbContext writeDbContext,
        CustomerCrmAccessService accessService,
        IDateTimeProvider dateTimeProvider)
    {
        _writeDbContext = writeDbContext;
        _accessService = accessService;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <summary>
    /// Tạo WorkPlan dài hạn có Customer reference primary và người phụ trách chính.
    /// </summary>
    public async Task<OperationResult<Guid>> Handle(CreateCustomerWorkPlanCommand request, CancellationToken cancellationToken)
    {
        var body = request.Request;
        var planName = Normalize(body.PlanName);
        if (body.CustomerId == Guid.Empty || planName is null || planName.Length > CustomerCrmTaskRules.MaxTitleLength ||
            !Enum.IsDefined(body.Status) || !Enum.IsDefined(body.Priority))
        {
            return OperationResult<Guid>.Fail("Customer, plan name, status or priority is invalid.");
        }

        if (body.StartDate.HasValue && body.EndDate.HasValue && body.EndDate < body.StartDate)
        {
            return OperationResult<Guid>.Fail("Plan end date cannot be before start date.");
        }

        var scope = await _accessService.BuildScopeAsync(cancellationToken);
        var customer = await GetVisibleCustomerForUpdateAsync(body.CustomerId, scope, cancellationToken);

        if (customer is null)
        {
            return OperationResult<Guid>.Fail("Customer was not found.");
        }

        var employee = await _accessService.ResolveEmployeeAsync(scope, body.AssignedSaleEmployeeId, true, cancellationToken);
        if (!employee.IsAllowed)
        {
            return OperationResult<Guid>.Fail("Assigned sale employee is outside your scope.");
        }

        var now = _dateTimeProvider.Now;
        var planId = Guid.CreateVersion7();
        await _writeDbContext.WorkPlans.AddAsync(new WorkPlan
        {
            Id = planId,
            CompanyId = scope.CompanyId,
            PlanName = planName,
            Objective = Normalize(body.Objective),
            Strategy = Normalize(body.Strategy),
            DiscussionSummary = Normalize(body.DiscussionSummary),
            NextAction = Normalize(body.NextAction),
            Status = body.Status,
            Priority = body.Priority,
            StartDate = body.StartDate,
            EndDate = body.EndDate,
            NextFollowUpDate = body.NextFollowUpDate,
            AssignedToEmployeeId = employee.EmployeeId,
            CreatedDate = now,
            CreatedBy = scope.EmployeeId,
            IsActive = true
        }, cancellationToken);

        await _writeDbContext.WorkPlanReferences.AddAsync(new WorkPlanReference
        {
            Id = Guid.CreateVersion7(),
            WorkPlanId = planId,
            ReferenceType = WorkReferenceType.Customer,
            ReferenceId = customer.CustomerId,
            ReferenceCodeSnapshot = customer.ExternalId,
            ReferenceNameSnapshot = customer.CustomerName,
            IsPrimary = true
        }, cancellationToken);

        if (employee.EmployeeId.HasValue)
        {
            await _writeDbContext.WorkPlanAssignees.AddAsync(new WorkPlanAssignee
            {
                Id = Guid.CreateVersion7(),
                WorkPlanId = planId,
                EmployeeId = employee.EmployeeId.Value,
                IsPrimary = true,
                IsActive = true,
                CreatedDate = now,
                CreatedBy = scope.EmployeeId
            }, cancellationToken);
        }

        await _writeDbContext.SaveChangesAsync(cancellationToken);
        return OperationResult<Guid>.Ok(planId);
    }

    private async Task<Customer?> GetVisibleCustomerForUpdateAsync(Guid customerId, ViewerScope scope, CancellationToken cancellationToken)
    {
        var visibleIds = _accessService.VisibleCustomers(scope).Select(x => x.CustomerId);
        return await _writeDbContext.Customers.FirstOrDefaultAsync(x =>
            x.CustomerId == customerId &&
            x.CompanyId == scope.CompanyId &&
            x.IsActive == true &&
            visibleIds.Contains(x.CustomerId), cancellationToken);
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
