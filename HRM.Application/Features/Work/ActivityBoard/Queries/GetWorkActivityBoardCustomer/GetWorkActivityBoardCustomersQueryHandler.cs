using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.Work.ActivityBoard.Dtos;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.WorkTaskEnums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Work.ActivityBoard.Queries.GetWorkActivityBoardCustomer;

internal sealed class GetWorkActivityBoardCustomersQueryHandler
    : IRequestHandler<GetWorkActivityBoardCustomersQuery, OperationResult<PagedResult<WorkActivityBoardCustomerSummaryDto>>>
{
    private readonly ICRMReadDbContext _dbContext;
    private readonly ICustomerVisibilityService _visibilityService;
    private readonly ICurrentUser _currentUser;

    public GetWorkActivityBoardCustomersQueryHandler(
        ICRMReadDbContext dbContext,
        ICustomerVisibilityService visibilityService,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _visibilityService = visibilityService;
        _currentUser = currentUser;
    }

    public async Task<OperationResult<PagedResult<WorkActivityBoardCustomerSummaryDto>>> Handle(
        GetWorkActivityBoardCustomersQuery query,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated)
        {
            throw new UnauthorizedAccessException();
        }

        var request = query.Request;
        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
        var pageNumber = request.NormalizedPageNumber;
        var pageSize = request.NormalizedPageSize;
        var activityTypes = request.ActivityTypes?.Count > 0
            ? request.ActivityTypes.ToHashSet()
            : Enum.GetValues<CustomerCrmCalendarSourceType>().ToHashSet();
        var includeFollowUpTasks = activityTypes.Contains(CustomerCrmCalendarSourceType.FollowUpTask);
        var includeInteractions = activityTypes.Contains(CustomerCrmCalendarSourceType.Interaction);
        var includeWorkPlans = activityTypes.Contains(CustomerCrmCalendarSourceType.WorkPlan);

        var customers = _visibilityService
            .ApplyCustomerVisibility(_dbContext.Customers.AsNoTracking(), scope);

        if (request.NormalizedKeyword is { } keyword)
        {
            customers = customers.Where(customer =>
                customer.ExternalId.Contains(keyword) ||
                customer.CustomerName.Contains(keyword));
        }

        var customerRows = customers
            .Select(customer => new CustomerPageRow
            {
                CustomerId = customer.CustomerId,
                ExternalId = customer.ExternalId,
                CustomerName = customer.CustomerName,
                IsLead = customer.IsLead,
                HasLeaderClaimAccess = customer.CustomerClaims.Any(claim =>
                    claim.IsActive &&
                    claim.Type == ClaimType.Work &&
                    claim.ExpiresAt > scope.Now &&
                    scope.LeaderGroupIds.Contains(claim.GroupId)),
                HasOpenTask = includeFollowUpTasks && _dbContext.WorkTasks
                    .AsNoTracking()
                    .Any(task =>
                        task.CompanyId == scope.CompanyId &&
                        (request.IncludeInactive || task.IsActive) &&
                        task.Status != WorkTaskStatus.Done &&
                        task.Status != WorkTaskStatus.Canceled &&
                        (!request.OnlyMine ||
                         task.CreatedBy == scope.EmployeeId ||
                         task.AssignedToEmployeeId == scope.EmployeeId ||
                         task.Assignees.Any(assignee =>
                             assignee.EmployeeId == scope.EmployeeId &&
                             assignee.IsActive)) &&
                        task.References.Any(reference =>
                            (reference.ReferenceType == WorkReferenceType.Customer &&
                             reference.ReferenceId == customer.CustomerId) ||
                            (reference.ReferenceType == WorkReferenceType.CustomerInteraction &&
                             _dbContext.CustomerInteractions.Any(interaction =>
                                 interaction.CompanyId == scope.CompanyId &&
                                 interaction.Id == reference.ReferenceId &&
                                 interaction.CustomerId == customer.CustomerId))) &&
                        (!customer.IsLead ||
                         scope.HasFullCustomerView ||
                         customer.CustomerClaims.Any(claim =>
                             claim.IsActive &&
                             claim.Type == ClaimType.Work &&
                             claim.ExpiresAt > scope.Now &&
                             scope.LeaderGroupIds.Contains(claim.GroupId)) ||
                         task.CreatedBy == scope.EmployeeId ||
                         task.AssignedToEmployeeId == scope.EmployeeId ||
                         task.Assignees.Any(assignee =>
                             assignee.EmployeeId == scope.EmployeeId &&
                             assignee.IsActive))),
                HasActivity =
                    includeFollowUpTasks && _dbContext.WorkTasks.AsNoTracking().Any(task =>
                        task.CompanyId == scope.CompanyId &&
                        (request.IncludeInactive || task.IsActive) &&
                        (request.IncludeCompleted ||
                         task.Status != WorkTaskStatus.Done &&
                         task.Status != WorkTaskStatus.Canceled) &&
                        (!request.OnlyMine ||
                         task.CreatedBy == scope.EmployeeId ||
                         task.AssignedToEmployeeId == scope.EmployeeId ||
                         task.Assignees.Any(assignee =>
                             assignee.EmployeeId == scope.EmployeeId &&
                             assignee.IsActive)) &&
                        task.References.Any(reference =>
                            (reference.ReferenceType == WorkReferenceType.Customer &&
                             reference.ReferenceId == customer.CustomerId) ||
                            (reference.ReferenceType == WorkReferenceType.CustomerInteraction &&
                             _dbContext.CustomerInteractions.Any(interaction =>
                                 interaction.CompanyId == scope.CompanyId &&
                                 interaction.Id == reference.ReferenceId &&
                                 interaction.CustomerId == customer.CustomerId))) &&
                        (!customer.IsLead ||
                         scope.HasFullCustomerView ||
                         customer.CustomerClaims.Any(claim =>
                             claim.IsActive &&
                             claim.Type == ClaimType.Work &&
                             claim.ExpiresAt > scope.Now &&
                             scope.LeaderGroupIds.Contains(claim.GroupId)) ||
                         task.CreatedBy == scope.EmployeeId ||
                         task.AssignedToEmployeeId == scope.EmployeeId ||
                         task.Assignees.Any(assignee =>
                             assignee.EmployeeId == scope.EmployeeId &&
                             assignee.IsActive))) ||
                    includeWorkPlans && _dbContext.WorkPlans.AsNoTracking().Any(plan =>
                        plan.CompanyId == scope.CompanyId &&
                        (request.IncludeInactive || plan.IsActive) &&
                        (request.IncludeCompleted ||
                         plan.Status != WorkPlanStatus.Completed &&
                         plan.Status != WorkPlanStatus.Canceled) &&
                        (!request.OnlyMine ||
                         plan.CreatedBy == scope.EmployeeId ||
                         plan.AssignedToEmployeeId == scope.EmployeeId ||
                         plan.Assignees.Any(assignee =>
                             assignee.EmployeeId == scope.EmployeeId &&
                             assignee.IsActive)) &&
                        plan.References.Any(reference =>
                            reference.ReferenceType == WorkReferenceType.Customer &&
                            reference.ReferenceId == customer.CustomerId) &&
                        (!customer.IsLead ||
                         scope.HasFullCustomerView ||
                         customer.CustomerClaims.Any(claim =>
                             claim.IsActive &&
                             claim.Type == ClaimType.Work &&
                             claim.ExpiresAt > scope.Now &&
                             scope.LeaderGroupIds.Contains(claim.GroupId)) ||
                         plan.CreatedBy == scope.EmployeeId ||
                         plan.AssignedToEmployeeId == scope.EmployeeId ||
                         plan.Assignees.Any(assignee =>
                             assignee.EmployeeId == scope.EmployeeId &&
                             assignee.IsActive))) ||
                    includeInteractions && _dbContext.CustomerInteractions.AsNoTracking().Any(interaction =>
                        interaction.CompanyId == scope.CompanyId &&
                        interaction.CustomerId == customer.CustomerId &&
                        (request.IncludeInactive || interaction.IsActive) &&
                        (!request.OnlyMine ||
                         interaction.CreatedBy == scope.EmployeeId ||
                         interaction.AssignedSaleEmployeeId == scope.EmployeeId) &&
                        (!customer.IsLead ||
                         scope.HasFullCustomerView ||
                         customer.CustomerClaims.Any(claim =>
                             claim.IsActive &&
                             claim.Type == ClaimType.Work &&
                             claim.ExpiresAt > scope.Now &&
                             scope.LeaderGroupIds.Contains(claim.GroupId)) ||
                         interaction.CreatedBy == scope.EmployeeId ||
                         interaction.AssignedSaleEmployeeId == scope.EmployeeId))
            });

        if (request.OnlyHasActivity)
        {
            customerRows = customerRows.Where(customer => customer.HasActivity);
        }

        var totalCount = await customerRows.CountAsync(cancellationToken);

        var pageCustomers = await customerRows
            .OrderByDescending(customer => customer.HasOpenTask)
            .ThenBy(customer => customer.CustomerName)
            .ThenBy(customer => customer.ExternalId)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        if (pageCustomers.Count == 0)
        {
            return OperationResult<PagedResult<WorkActivityBoardCustomerSummaryDto>>.Ok(
                new PagedResult<WorkActivityBoardCustomerSummaryDto>(
                    Array.Empty<WorkActivityBoardCustomerSummaryDto>(),
                    totalCount,
                    pageNumber,
                    pageSize));
        }

        var customerIds = pageCustomers.Select(customer => customer.CustomerId).ToArray();
        var interactionCustomerMap = await _dbContext.CustomerInteractions
            .AsNoTracking()
            .Where(interaction =>
                interaction.CompanyId == scope.CompanyId &&
                customerIds.Contains(interaction.CustomerId))
            .Select(interaction => new
            {
                interaction.Id,
                interaction.CustomerId
            })
            .ToListAsync(cancellationToken);

        var interactionCustomerById = interactionCustomerMap
            .GroupBy(interaction => interaction.Id)
            .ToDictionary(group => group.Key, group => group.First().CustomerId);
        var interactionIds = interactionCustomerById.Keys.ToArray();

        var tasks = includeFollowUpTasks
            ? await _dbContext.WorkTasks
                .AsNoTracking()
                .Where(task =>
                    task.CompanyId == scope.CompanyId &&
                    (request.IncludeInactive || task.IsActive) &&
                    (!request.OnlyMine ||
                     task.CreatedBy == scope.EmployeeId ||
                     task.AssignedToEmployeeId == scope.EmployeeId ||
                     task.Assignees.Any(assignee =>
                         assignee.EmployeeId == scope.EmployeeId &&
                         assignee.IsActive)) &&
                    task.References.Any(reference =>
                        (reference.ReferenceType == WorkReferenceType.Customer &&
                         customerIds.Contains(reference.ReferenceId)) ||
                        (reference.ReferenceType == WorkReferenceType.CustomerInteraction &&
                         interactionIds.Contains(reference.ReferenceId))))
                .Select(task => new TaskActivityRow
                {
                    TaskId = task.Id,
                    CustomerId = task.References
                        .Where(reference =>
                            reference.ReferenceType == WorkReferenceType.Customer &&
                            customerIds.Contains(reference.ReferenceId))
                        .Select(reference => (Guid?)reference.ReferenceId)
                        .FirstOrDefault(),
                    InteractionId = task.References
                        .Where(reference =>
                            reference.ReferenceType == WorkReferenceType.CustomerInteraction &&
                            interactionIds.Contains(reference.ReferenceId))
                        .Select(reference => (Guid?)reference.ReferenceId)
                        .FirstOrDefault(),
                    Status = task.Status,
                    DueDate = task.DueDate,
                    CompletedDate = task.CompletedDate,
                    CreatedDate = task.CreatedDate,
                    CreatedBy = task.CreatedBy,
                    AssignedToEmployeeId = task.AssignedToEmployeeId,
                    IsAssignedToCurrentEmployee = task.Assignees.Any(assignee =>
                        assignee.EmployeeId == scope.EmployeeId &&
                        assignee.IsActive)
                })
                .ToListAsync(cancellationToken)
            : new List<TaskActivityRow>();

        foreach (var task in tasks.Where(task => !task.CustomerId.HasValue && task.InteractionId.HasValue))
        {
            if (interactionCustomerById.TryGetValue(task.InteractionId!.Value, out var customerId))
            {
                task.CustomerId = customerId;
            }
        }

        var plans = includeWorkPlans
            ? await _dbContext.WorkPlans
                .AsNoTracking()
                .Where(plan =>
                    plan.CompanyId == scope.CompanyId &&
                    (request.IncludeInactive || plan.IsActive) &&
                    (!request.OnlyMine ||
                     plan.CreatedBy == scope.EmployeeId ||
                     plan.AssignedToEmployeeId == scope.EmployeeId ||
                     plan.Assignees.Any(assignee =>
                         assignee.EmployeeId == scope.EmployeeId &&
                         assignee.IsActive)) &&
                    plan.References.Any(reference =>
                        reference.ReferenceType == WorkReferenceType.Customer &&
                        customerIds.Contains(reference.ReferenceId)))
                .Select(plan => new PlanActivityRow
                {
                    PlanId = plan.Id,
                    CustomerId = plan.References
                        .Where(reference =>
                            reference.ReferenceType == WorkReferenceType.Customer &&
                            customerIds.Contains(reference.ReferenceId))
                        .Select(reference => reference.ReferenceId)
                        .FirstOrDefault(),
                    Status = plan.Status,
                    NextFollowUpDate = plan.NextFollowUpDate,
                    EndDate = plan.EndDate,
                    CreatedDate = plan.CreatedDate,
                    CreatedBy = plan.CreatedBy,
                    AssignedToEmployeeId = plan.AssignedToEmployeeId,
                    IsAssignedToCurrentEmployee = plan.Assignees.Any(assignee =>
                        assignee.EmployeeId == scope.EmployeeId &&
                        assignee.IsActive)
                })
                .ToListAsync(cancellationToken)
            : new List<PlanActivityRow>();

        var interactions = includeInteractions
            ? await _dbContext.CustomerInteractions
                .AsNoTracking()
                .Where(interaction =>
                    interaction.CompanyId == scope.CompanyId &&
                    customerIds.Contains(interaction.CustomerId) &&
                    (request.IncludeInactive || interaction.IsActive) &&
                    (!request.OnlyMine ||
                     interaction.CreatedBy == scope.EmployeeId ||
                     interaction.AssignedSaleEmployeeId == scope.EmployeeId))
                .Select(interaction => new InteractionActivityRow
                {
                    InteractionId = interaction.Id,
                    CustomerId = interaction.CustomerId,
                    InteractionAt = interaction.InteractionAt,
                    CreatedBy = interaction.CreatedBy,
                    AssignedSaleEmployeeId = interaction.AssignedSaleEmployeeId
                })
                .ToListAsync(cancellationToken)
            : new List<InteractionActivityRow>();

        var summaries = pageCustomers
            .Select(customer => BuildSummary(
                customer,
                tasks,
                plans,
                interactions,
                request,
                scope.EmployeeId,
                scope.HasFullCustomerView,
                scope.Now.Date))
            .ToList();

        return OperationResult<PagedResult<WorkActivityBoardCustomerSummaryDto>>.Ok(
            new PagedResult<WorkActivityBoardCustomerSummaryDto>(
                summaries,
                totalCount,
                pageNumber,
                pageSize));
    }

    private static WorkActivityBoardCustomerSummaryDto BuildSummary(
        CustomerPageRow customer,
        IReadOnlyList<TaskActivityRow> allTasks,
        IReadOnlyList<PlanActivityRow> allPlans,
        IReadOnlyList<InteractionActivityRow> allInteractions,
        WorkActivityBoardCustomerListQueryDto request,
        Guid employeeId,
        bool hasFullCustomerView,
        DateTime today)
    {
        var tasks = allTasks
            .Where(task => task.CustomerId == customer.CustomerId)
            .Where(task => !request.OnlyMine || IsMine(task.CreatedBy, task.AssignedToEmployeeId, task.IsAssignedToCurrentEmployee, employeeId))
            .Where(task => CanSeeLeadActivity(customer, hasFullCustomerView, task.CreatedBy, task.AssignedToEmployeeId, task.IsAssignedToCurrentEmployee, employeeId))
            .ToList();
        var plans = allPlans
            .Where(plan => plan.CustomerId == customer.CustomerId)
            .Where(plan => !request.OnlyMine || IsMine(plan.CreatedBy, plan.AssignedToEmployeeId, plan.IsAssignedToCurrentEmployee, employeeId))
            .Where(plan => CanSeeLeadActivity(customer, hasFullCustomerView, plan.CreatedBy, plan.AssignedToEmployeeId, plan.IsAssignedToCurrentEmployee, employeeId))
            .ToList();
        var interactions = allInteractions
            .Where(interaction => interaction.CustomerId == customer.CustomerId)
            .Where(interaction => !request.OnlyMine || IsMine(interaction.CreatedBy, interaction.AssignedSaleEmployeeId, false, employeeId))
            .Where(interaction => CanSeeLeadActivity(customer, hasFullCustomerView, interaction.CreatedBy, interaction.AssignedSaleEmployeeId, false, employeeId))
            .ToList();

        var openTaskCount = tasks.Count(task => !IsTaskCompleted(task.Status));
        var completedTaskCount = request.IncludeCompleted
            ? tasks.Count(task => IsTaskCompleted(task.Status))
            : 0;
        var visiblePlans = request.IncludeCompleted
            ? plans
            : plans.Where(plan => !IsPlanCompleted(plan.Status)).ToList();
        var overdueCount = tasks.Count(task =>
                               task.DueDate.HasValue &&
                               task.DueDate.Value.Date < today &&
                               !IsTaskCompleted(task.Status)) +
                           visiblePlans.Count(plan =>
                               plan.NextFollowUpDate.HasValue &&
                               plan.NextFollowUpDate.Value.Date < today &&
                               !IsPlanCompleted(plan.Status));

        return new WorkActivityBoardCustomerSummaryDto
        {
            CustomerId = customer.CustomerId,
            ExternalId = customer.ExternalId,
            CustomerName = customer.CustomerName,
            IsLead = customer.IsLead,
            LastActivityAt = MaxDate(
                tasks.Select(task => (DateTime?)(task.CompletedDate ?? task.DueDate ?? task.CreatedDate)),
                visiblePlans.Select(plan => (DateTime?)(plan.NextFollowUpDate ?? plan.EndDate ?? plan.CreatedDate)),
                interactions.Select(interaction => (DateTime?)interaction.InteractionAt)),
            OpenTaskCount = openTaskCount,
            CompletedTaskCount = completedTaskCount,
            WorkPlanCount = visiblePlans.Count,
            InteractionCount = interactions.Count,
            OverdueCount = overdueCount
        };
    }

    private static bool CanSeeLeadActivity(
        CustomerPageRow customer,
        bool hasFullCustomerView,
        Guid createdBy,
        Guid? assignedEmployeeId,
        bool isAssignedToCurrentEmployee,
        Guid employeeId)
        => !customer.IsLead ||
           hasFullCustomerView ||
           customer.HasLeaderClaimAccess ||
           createdBy == employeeId ||
           assignedEmployeeId == employeeId ||
           isAssignedToCurrentEmployee;

    private static bool IsMine(
        Guid createdBy,
        Guid? assignedEmployeeId,
        bool isAssignedToCurrentEmployee,
        Guid employeeId)
        => createdBy == employeeId ||
           assignedEmployeeId == employeeId ||
           isAssignedToCurrentEmployee;

    private static bool IsTaskCompleted(WorkTaskStatus status)
        => status is WorkTaskStatus.Done or WorkTaskStatus.Canceled;

    private static bool IsPlanCompleted(WorkPlanStatus status)
        => status is WorkPlanStatus.Completed or WorkPlanStatus.Canceled;

    private static DateTime? MaxDate(params IEnumerable<DateTime?>[] sources)
        => sources
            .SelectMany(source => source)
            .Where(value => value.HasValue)
            .DefaultIfEmpty()
            .Max();

    private sealed class CustomerPageRow
    {
        public Guid CustomerId { get; set; }
        public string ExternalId { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public bool IsLead { get; set; }
        public bool HasLeaderClaimAccess { get; set; }
        public bool HasOpenTask { get; set; }
        public bool HasActivity { get; set; }
    }

    private sealed class TaskActivityRow
    {
        public Guid TaskId { get; set; }
        public Guid? CustomerId { get; set; }
        public Guid? InteractionId { get; set; }
        public WorkTaskStatus Status { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public DateTime CreatedDate { get; set; }
        public Guid CreatedBy { get; set; }
        public Guid? AssignedToEmployeeId { get; set; }
        public bool IsAssignedToCurrentEmployee { get; set; }
    }

    private sealed class PlanActivityRow
    {
        public Guid PlanId { get; set; }
        public Guid CustomerId { get; set; }
        public WorkPlanStatus Status { get; set; }
        public DateTime? NextFollowUpDate { get; set; }
        public DateTime? EndDate { get; set; }
        public DateTime CreatedDate { get; set; }
        public Guid CreatedBy { get; set; }
        public Guid? AssignedToEmployeeId { get; set; }
        public bool IsAssignedToCurrentEmployee { get; set; }
    }

    private sealed class InteractionActivityRow
    {
        public Guid InteractionId { get; set; }
        public Guid CustomerId { get; set; }
        public DateTime InteractionAt { get; set; }
        public Guid CreatedBy { get; set; }
        public Guid? AssignedSaleEmployeeId { get; set; }
    }
}
