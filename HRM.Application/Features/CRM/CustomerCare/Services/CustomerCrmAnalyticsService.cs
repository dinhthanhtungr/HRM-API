using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Commons.Reporting;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using HRM.Application.Features.CRM.CustomerCare.Models;
using HRM.Domain.Entities.WorkTaskSchema;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.WorkTaskEnums;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.CustomerCare.Services;

/// <summary>
/// Tổng hợp dữ liệu CRM thành calendar, activity header, dashboard và báo cáo hoạt động.
/// Mọi nguồn dữ liệu đều được giới hạn theo company và customer visibility scope hiện tại.
/// </summary>
internal sealed class CustomerCrmAnalyticsService : ICustomerCrmAnalyticsService
{
    private const decimal HighRevenueThreshold = 100_000_000m;
    private const decimal MediumRevenueThreshold = 50_000_000m;
    private readonly ICRMReadDbContext _dbContext;
    private readonly CustomerCrmAccessService _accessService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public CustomerCrmAnalyticsService(
        ICRMReadDbContext dbContext,
        CustomerCrmAccessService accessService,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _accessService = accessService;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <summary>
    /// Hợp nhất hạn WorkTask, thời điểm CustomerInteraction và ngày follow-up WorkPlan thành sự kiện calendar.
    /// Không tạo hoặc đọc bảng calendar riêng.
    /// </summary>
    public async Task<OperationResult<IReadOnlyList<CustomerCrmCalendarEventDto>>> GetCalendarAsync(
        CustomerCrmCalendarQuery query,
        CancellationToken cancellationToken = default)
    {
        var scope = await _accessService.BuildScopeAsync(cancellationToken);
        var range = ResolveCalendarRange(query, _dateTimeProvider.Now);

        if (!range.IsValid)
            return OperationResult<IReadOnlyList<CustomerCrmCalendarEventDto>>.Fail(range.Error!);
        var employee = await _accessService.ResolveEmployeeAsync(scope, query.AssignedSaleEmployeeId, query.OnlyMine, cancellationToken);
        if (!employee.IsAllowed)
            return OperationResult<IReadOnlyList<CustomerCrmCalendarEventDto>>.Fail("Assigned sale employee is outside your scope.");

        var group = await ResolveGroupMemberEmployeeIdsAsync(scope, query.GroupId, cancellationToken);
        if (!group.IsAllowed)
            return OperationResult<IReadOnlyList<CustomerCrmCalendarEventDto>>.Fail("Group is outside your scope.");

        var types = query.ActivityTypes?.Count > 0
            ? query.ActivityTypes.ToHashSet()
            : Enum.GetValues<CustomerCrmCalendarSourceType>().ToHashSet();

        var visibleIds = _accessService.VisibleCustomers(scope).Select(x => x.CustomerId);

        Guid? selectedCustomerId = null;

        if (query.CustomerId.HasValue)
        {
            if (query.CustomerId.Value == Guid.Empty)
            {
                return OperationResult<IReadOnlyList<CustomerCrmCalendarEventDto>>
                    .Fail("Customer id is invalid.");
            }

            var isVisible = await visibleIds.ContainsAsync(
                query.CustomerId.Value,
                cancellationToken);

            if (!isVisible)
            {
                return OperationResult<IReadOnlyList<CustomerCrmCalendarEventDto>>
                    .Fail("Customer was not found or is outside your scope.");
            }

            selectedCustomerId = query.CustomerId.Value;
        }

        var events = new List<CustomerCrmCalendarEventDto>();
        var today = _dateTimeProvider.Now.Date;

        if (types.Contains(CustomerCrmCalendarSourceType.FollowUpTask))
        {
            var visibleInteractionIds = _dbContext.CustomerInteractions
                .AsNoTracking()
                .Where(interaction =>
                    interaction.CompanyId == scope.CompanyId &&
                    visibleIds.Contains(interaction.CustomerId))
                .Select(interaction => interaction.Id);

            var tasks = _dbContext.WorkTasks.AsNoTracking().Where(x =>
                x.CompanyId == scope.CompanyId && 
                x.IsActive && 
                x.DueDate.HasValue &&
                x.DueDate >= range.From && 
                x.DueDate < range.ToExclusive &&
                x.References.Any(reference =>
                    (reference.ReferenceType == WorkReferenceType.Customer &&
                     visibleIds.Contains(reference.ReferenceId)) ||
                    (reference.ReferenceType == WorkReferenceType.CustomerInteraction &&
                     visibleInteractionIds.Contains(reference.ReferenceId))));

            if (employee.EmployeeId.HasValue) tasks = tasks.Where(x => x.AssignedToEmployeeId == employee.EmployeeId);
            if (group.EmployeeIds is { } groupEmployeeIds)
            {
                tasks = tasks.Where(x =>
                    x.AssignedToEmployeeId.HasValue &&
                    groupEmployeeIds.Contains(x.AssignedToEmployeeId.Value));
            }

            if (!query.ShowCompletedTasks) tasks = tasks.Where(x => x.Status != WorkTaskStatus.Done && x.Status != WorkTaskStatus.Canceled);

            if (selectedCustomerId.HasValue)
            {
                var customerId = selectedCustomerId.Value;

                var customerInteractionIds = _dbContext.CustomerInteractions
                    .AsNoTracking()
                    .Where(interaction =>
                        interaction.CompanyId == scope.CompanyId &&
                        interaction.CustomerId == customerId)
                    .Select(interaction => interaction.Id);

                tasks = tasks.Where(task =>
                    task.References.Any(reference =>
                        (reference.ReferenceType == WorkReferenceType.Customer &&
                         reference.ReferenceId == customerId) ||
                        (reference.ReferenceType == WorkReferenceType.CustomerInteraction &&
                         customerInteractionIds.Contains(reference.ReferenceId))));
            }


            var rows = await tasks.Select(x => new
            {
                x.Id, 
                x.Title, 
                x.Description, 
                x.DueDate, 
                x.Status, 
                x.Priority,
                x.AssignedToEmployeeId,
                EmployeeName = x.AssignedToEmployee != null ? x.AssignedToEmployee.FullName : null,
                CustomerId = x.References
                    .Where(reference => reference.ReferenceType == WorkReferenceType.Customer)
                    .OrderByDescending(reference => reference.IsPrimary)
                    .Select(reference => (Guid?)reference.ReferenceId)
                    .FirstOrDefault() ??
                    _dbContext.CustomerInteractions
                        .Where(interaction =>
                            interaction.CompanyId == x.CompanyId &&
                            x.References.Any(reference =>
                                reference.ReferenceType == WorkReferenceType.CustomerInteraction &&
                                reference.ReferenceId == interaction.Id))
                        .Select(interaction => (Guid?)interaction.CustomerId)
                        .FirstOrDefault(),
                CustomerExternalId = x.References
                    .Where(reference => reference.ReferenceType == WorkReferenceType.Customer)
                    .OrderByDescending(reference => reference.IsPrimary)
                    .Select(reference => reference.ReferenceCodeSnapshot)
                    .FirstOrDefault() ??
                    _dbContext.CustomerInteractions
                        .Where(interaction =>
                            interaction.CompanyId == x.CompanyId &&
                            x.References.Any(reference =>
                                reference.ReferenceType == WorkReferenceType.CustomerInteraction &&
                                reference.ReferenceId == interaction.Id))
                        .Select(interaction => interaction.Customer.ExternalId)
                        .FirstOrDefault(),
                CustomerName = x.References
                    .Where(reference => reference.ReferenceType == WorkReferenceType.Customer)
                    .OrderByDescending(reference => reference.IsPrimary)
                    .Select(reference => reference.ReferenceNameSnapshot)
                    .FirstOrDefault() ??
                    _dbContext.CustomerInteractions
                        .Where(interaction =>
                            interaction.CompanyId == x.CompanyId &&
                            x.References.Any(reference =>
                                reference.ReferenceType == WorkReferenceType.CustomerInteraction &&
                                reference.ReferenceId == interaction.Id))
                        .Select(interaction => interaction.Customer.CustomerName)
                        .FirstOrDefault()
            }).ToListAsync(cancellationToken);

            events.AddRange(rows.Where(x => x.CustomerId.HasValue).Select(x =>
            {
                var overdue = x.DueDate!.Value.Date < today && !CustomerCrmTaskRules.IsTerminal(x.Status);
                return new CustomerCrmCalendarEventDto
                {
                    Id = BuildCalendarEventId(CustomerCrmCalendarSourceType.FollowUpTask, x.Id),
                    SourceId = x.Id,
                    SourceType = CustomerCrmCalendarSourceType.FollowUpTask,
                    CustomerId = x.CustomerId!.Value,
                    CustomerExternalId = x.CustomerExternalId ?? string.Empty,
                    CustomerName = x.CustomerName ?? string.Empty,
                    AssignedSaleEmployeeId = x.AssignedToEmployeeId,
                    AssignedSaleEmployeeName = x.EmployeeName, Title = x.Title, Description = x.Description,
                    Start = x.DueDate.Value, End = x.DueDate.Value.AddMinutes(30), StatusCode = x.Status.ToString(),
                    PriorityCode = x.Priority.ToString(), IsOverdue = overdue,
                    ColorKey = CustomerCrmTaskRules.ResolveTaskColor(x.Status, x.Priority, overdue)
                };
            }));
        }

        if (types.Contains(CustomerCrmCalendarSourceType.Interaction))
        {
            var interactions = _dbContext.CustomerInteractions.AsNoTracking().Where(x =>
                x.CompanyId == scope.CompanyId && x.IsActive && visibleIds.Contains(x.CustomerId) &&
                x.InteractionAt >= range.From && x.InteractionAt < range.ToExclusive);
            if (employee.EmployeeId.HasValue) interactions = interactions.Where(x => x.AssignedSaleEmployeeId == employee.EmployeeId);
            if (group.EmployeeIds is { } groupEmployeeIds)
            {
                interactions = interactions.Where(x =>
                    x.AssignedSaleEmployeeId.HasValue &&
                    groupEmployeeIds.Contains(x.AssignedSaleEmployeeId.Value));
            }
            if (selectedCustomerId.HasValue) interactions = interactions.Where(x => x.CustomerId == selectedCustomerId.Value);
            var rows = await interactions.Select(x => new
            {
                x.Id, x.CustomerId, x.Customer.ExternalId, x.Customer.CustomerName, x.AssignedSaleEmployeeId,
                EmployeeName = x.AssignedSaleEmployee != null ? x.AssignedSaleEmployee.FullName : null,
                x.Subject, x.Content, x.InteractionAt, x.InteractionType
            }).ToListAsync(cancellationToken);
            events.AddRange(rows.Select(x => new CustomerCrmCalendarEventDto
            {
                Id = BuildCalendarEventId(CustomerCrmCalendarSourceType.Interaction, x.Id),
                SourceId = x.Id,
                SourceType = CustomerCrmCalendarSourceType.Interaction,
                CustomerId = x.CustomerId, CustomerExternalId = x.ExternalId, CustomerName = x.CustomerName,
                AssignedSaleEmployeeId = x.AssignedSaleEmployeeId, AssignedSaleEmployeeName = x.EmployeeName,
                Title = x.Subject ?? x.InteractionType.ToString(), Description = x.Content,
                Start = x.InteractionAt, End = x.InteractionAt.AddMinutes(30), StatusCode = x.InteractionType.ToString(),
                PriorityCode = string.Empty, IsOverdue = false,
                ColorKey = CustomerCrmTaskRules.ResolveInteractionColor(x.InteractionType)
            }));
        }

        if (types.Contains(CustomerCrmCalendarSourceType.WorkPlan))
        {
            var plans = _dbContext.WorkPlans.AsNoTracking().Where(x =>
                x.CompanyId == scope.CompanyId && 
                x.IsActive && 
                x.NextFollowUpDate.HasValue &&
                x.NextFollowUpDate >= range.From && 
                x.NextFollowUpDate < range.ToExclusive &&
                x.References.Any(r => r.ReferenceType == WorkReferenceType.Customer && 
                visibleIds.Contains(r.ReferenceId)));

            if (employee.EmployeeId.HasValue) plans = plans.Where(x => x.AssignedToEmployeeId == employee.EmployeeId);
            if (group.EmployeeIds is { } groupEmployeeIds)
            {
                plans = plans.Where(x =>
                    x.AssignedToEmployeeId.HasValue &&
                    groupEmployeeIds.Contains(x.AssignedToEmployeeId.Value));
            }
            if (selectedCustomerId.HasValue)
            {
                plans = plans.Where(plan => plan.References.Any(reference =>
                    reference.ReferenceType == WorkReferenceType.Customer &&
                    reference.ReferenceId == selectedCustomerId.Value));
            }
            var rows = await plans.Select(x => new
            {
                x.Id, 
                x.PlanName, 
                x.Objective, 
                x.NextFollowUpDate, 
                x.Status, 
                x.Priority, 
                x.AssignedToEmployeeId,
                EmployeeName = x.AssignedToEmployee != null ? x.AssignedToEmployee.FullName : null,
                Customer = x.References.Where(r => r.ReferenceType == WorkReferenceType.Customer && r.IsPrimary)
                    .Select(r => new { Id = r.ReferenceId, Code = r.ReferenceCodeSnapshot, Name = r.ReferenceNameSnapshot }).FirstOrDefault()
            }).ToListAsync(cancellationToken);

            events.AddRange(rows.Where(x => x.Customer != null).Select(x => new CustomerCrmCalendarEventDto
            {
                Id = BuildCalendarEventId(CustomerCrmCalendarSourceType.WorkPlan, x.Id),
                SourceId = x.Id,
                SourceType = CustomerCrmCalendarSourceType.WorkPlan,
                CustomerId = x.Customer!.Id, CustomerExternalId = x.Customer.Code ?? string.Empty,
                CustomerName = x.Customer.Name ?? string.Empty, AssignedSaleEmployeeId = x.AssignedToEmployeeId,
                AssignedSaleEmployeeName = x.EmployeeName, Title = x.PlanName, Description = x.Objective,
                Start = x.NextFollowUpDate!.Value, End = x.NextFollowUpDate.Value.AddMinutes(30),
                StatusCode = x.Status.ToString(), PriorityCode = x.Priority.ToString(),
                IsOverdue = x.NextFollowUpDate.Value.Date < today && x.Status is WorkPlanStatus.Active or WorkPlanStatus.Paused,
                ColorKey = EventTypeColorKey.WorkPlan
            }));
        }

        if (types.Contains(CustomerCrmCalendarSourceType.PersonalTask) &&
            !selectedCustomerId.HasValue &&
            (!employee.EmployeeId.HasValue || employee.EmployeeId.Value == scope.EmployeeId) &&
            (group.EmployeeIds is null || group.EmployeeIds.Contains(scope.EmployeeId)))
        {
            var personalTasks = _dbContext.WorkTasks.AsNoTracking().Where(x =>
                x.CompanyId == scope.CompanyId &&
                x.IsActive &&
                x.AssignedToEmployeeId == scope.EmployeeId &&
                x.DueDate.HasValue &&
                x.DueDate >= range.From &&
                x.DueDate < range.ToExclusive &&
                !x.References.Any());

            if (!query.ShowCompletedTasks)
            {
                personalTasks = personalTasks.Where(x =>
                    x.Status != WorkTaskStatus.Done &&
                    x.Status != WorkTaskStatus.Canceled);
            }

            var rows = await personalTasks.Select(x => new
            {
                x.Id,
                x.Title,
                x.Description,
                x.DueDate,
                x.Status,
                x.Priority,
                x.AssignedToEmployeeId,
                EmployeeName = x.AssignedToEmployee != null ? x.AssignedToEmployee.FullName : null
            }).ToListAsync(cancellationToken);

            events.AddRange(rows.Select(x =>
            {
                var overdue = x.DueDate!.Value.Date < today && !CustomerCrmTaskRules.IsTerminal(x.Status);
                return new CustomerCrmCalendarEventDto
                {
                    Id = BuildCalendarEventId(CustomerCrmCalendarSourceType.PersonalTask, x.Id),
                    SourceId = x.Id,
                    SourceType = CustomerCrmCalendarSourceType.PersonalTask,
                    CustomerId = null,
                    CustomerExternalId = string.Empty,
                    CustomerName = string.Empty,
                    AssignedSaleEmployeeId = x.AssignedToEmployeeId,
                    AssignedSaleEmployeeName = x.EmployeeName,
                    Title = x.Title,
                    Description = x.Description,
                    Start = x.DueDate.Value,
                    End = x.DueDate.Value.AddMinutes(30),
                    StatusCode = x.Status.ToString(),
                    PriorityCode = x.Priority.ToString(),
                    IsOverdue = overdue,
                    ColorKey = CustomerCrmTaskRules.ResolveTaskColor(x.Status, x.Priority, overdue)
                };
            }));
        }

        if (!query.ShowWeekends)
            events = events.Where(x => x.Start.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)).ToList();

        return OperationResult<IReadOnlyList<CustomerCrmCalendarEventDto>>.Ok(
            events.OrderBy(x => x.Start).ThenBy(x => x.SourceType).ThenBy(x => x.SourceId).ToList());
    }

    /// <summary>
    /// Tạo danh sách tổng quan hoạt động theo khách hàng, ưu tiên khách có task quá hạn hoặc task đang mở.
    /// </summary>
    public async Task<OperationResult<PagedResult<CustomerCrmActivityHeaderDto>>> GetActivityHeadersAsync(
        CustomerCrmActivityHeaderQuery query,
        CancellationToken cancellationToken = default)
    {
        var scope = await _accessService.BuildScopeAsync(cancellationToken);
        var employee = await _accessService.ResolveEmployeeAsync(scope, query.AssignedSaleEmployeeId, query.OnlyMine, cancellationToken);
        if (!employee.IsAllowed) return OperationResult<PagedResult<CustomerCrmActivityHeaderDto>>.Fail("Employee is outside your scope.");
        var group = await ResolveGroupMemberEmployeeIdsAsync(scope, query.GroupId, cancellationToken);
        if (!group.IsAllowed) return OperationResult<PagedResult<CustomerCrmActivityHeaderDto>>.Fail("Group is outside your scope.");
        var customers = _accessService.VisibleCustomers(scope);
        if (query.CustomerId.HasValue) customers = customers.Where(x => x.CustomerId == query.CustomerId);
        if (group.EmployeeIds is { } groupEmployeeIds)
        {
            customers = customers.Where(x =>
                groupEmployeeIds.Contains(x.CurrentSaleId ?? Guid.Empty) ||
                x.CustomerAssignments.Any(a => a.IsActive && groupEmployeeIds.Contains(a.EmployeeId)) ||
                x.CustomerClaims.Any(cl =>
                    cl.IsActive &&
                    cl.Type == ClaimType.Work &&
                    cl.ExpiresAt > scope.Now &&
                    groupEmployeeIds.Contains(cl.EmployeeId)));
        }
        if (!string.IsNullOrWhiteSpace(query.NormalizedKeyword))
        {
            var keyword = query.NormalizedKeyword;
            customers = customers.Where(x => x.ExternalId.Contains(keyword) || x.CustomerName.Contains(keyword));
        }
        var customerRows = await customers.Select(x => new { x.CustomerId, x.ExternalId, x.CustomerName }).ToListAsync(cancellationToken);
        var ids = customerRows.Select(x => x.CustomerId).ToArray();
        var today = _dateTimeProvider.Now.Date;
        var types = query.ActivityTypes?.Count > 0
            ? query.ActivityTypes.ToHashSet()
            : new HashSet<CustomerCrmCalendarSourceType>
            {
                CustomerCrmCalendarSourceType.FollowUpTask,
                CustomerCrmCalendarSourceType.Interaction
            };
        var taskRows = new List<ActivityTaskRow>();
        if (types.Contains(CustomerCrmCalendarSourceType.FollowUpTask))
        {
            var taskQuery = _dbContext.WorkTasks.AsNoTracking().Where(x => x.CompanyId == scope.CompanyId && x.IsActive &&
                x.References.Any(r => r.ReferenceType == WorkReferenceType.Customer && ids.Contains(r.ReferenceId)));
            if (employee.EmployeeId.HasValue) taskQuery = taskQuery.Where(x => x.AssignedToEmployeeId == employee.EmployeeId);
            if (group.EmployeeIds is { } taskGroupEmployeeIds)
            {
                taskQuery = taskQuery.Where(x =>
                    x.AssignedToEmployeeId.HasValue &&
                    taskGroupEmployeeIds.Contains(x.AssignedToEmployeeId.Value));
            }
            if (!query.IncludeCompleted) taskQuery = taskQuery.Where(x => x.Status != WorkTaskStatus.Done);
            if (!query.IncludeCanceled) taskQuery = taskQuery.Where(x => x.Status != WorkTaskStatus.Canceled);
            taskRows = await taskQuery.Select(x => new ActivityTaskRow
            {
                CustomerId = x.References.Where(r => r.ReferenceType == WorkReferenceType.Customer && r.IsPrimary).Select(r => r.ReferenceId).First(),
                Status = x.Status, DueDate = x.DueDate, ActivityDate = x.UpdatedDate ?? x.CreatedDate
            }).ToListAsync(cancellationToken);
        }
        var interactionRows = new List<ActivityInteractionRow>();
        if (types.Contains(CustomerCrmCalendarSourceType.Interaction))
        {
            var interactionQuery = _dbContext.CustomerInteractions.AsNoTracking().Where(x => x.CompanyId == scope.CompanyId && x.IsActive && ids.Contains(x.CustomerId));
            if (employee.EmployeeId.HasValue) interactionQuery = interactionQuery.Where(x => x.AssignedSaleEmployeeId == employee.EmployeeId);
            if (group.EmployeeIds is { } interactionGroupEmployeeIds)
            {
                interactionQuery = interactionQuery.Where(x =>
                    x.AssignedSaleEmployeeId.HasValue &&
                    interactionGroupEmployeeIds.Contains(x.AssignedSaleEmployeeId.Value));
            }
            interactionRows = await interactionQuery.Select(x => new ActivityInteractionRow { CustomerId = x.CustomerId, InteractionAt = x.InteractionAt }).ToListAsync(cancellationToken);
        }
        var headers = customerRows.Select(customer =>
        {
            var tasks = taskRows.Where(x => x.CustomerId == customer.CustomerId).ToArray();
            var interactions = interactionRows.Where(x => x.CustomerId == customer.CustomerId).ToArray();
            return new CustomerCrmActivityHeaderDto
            {
                CustomerId = customer.CustomerId, CustomerExternalId = customer.ExternalId, CustomerName = customer.CustomerName,
                TotalCount = tasks.Length + interactions.Length,
                OpenTaskCount = tasks.Count(x => CustomerCrmTaskRules.IsOpen(x.Status)),
                CompletedTaskCount = tasks.Count(x => x.Status == WorkTaskStatus.Done),
                CanceledTaskCount = tasks.Count(x => x.Status == WorkTaskStatus.Canceled),
                OverdueTaskCount = tasks.Count(x => x.DueDate.HasValue && x.DueDate.Value.Date < today && !CustomerCrmTaskRules.IsTerminal(x.Status)),
                InteractionCount = interactions.Length,
                LastActivityDate = tasks.Select(x => (DateTime?)x.ActivityDate).Concat(interactions.Select(x => (DateTime?)x.InteractionAt)).Max()
            };
        }).Where(x => x.TotalCount > 0).OrderByDescending(x => x.OverdueTaskCount).ThenByDescending(x => x.OpenTaskCount)
          .ThenByDescending(x => x.InteractionCount).ThenByDescending(x => x.LastActivityDate).ToList();
        var total = headers.Count;
        var items = headers.Skip((query.NormalizedPageNumber - 1) * query.NormalizedPageSize).Take(query.NormalizedPageSize).ToList();
        return OperationResult<PagedResult<CustomerCrmActivityHeaderDto>>.Ok(
            new PagedResult<CustomerCrmActivityHeaderDto>(items, total, query.NormalizedPageNumber, query.NormalizedPageSize));
    }

    /// <summary>
    /// Trả dashboard cá nhân của sale hiện tại từ task được giao và interaction trong customer scope.
    /// </summary>
    public async Task<OperationResult<SaleCrmDashboardDto>> GetSaleDashboardAsync(CancellationToken cancellationToken = default)
    {
        var scope = await _accessService.BuildScopeAsync(cancellationToken);
        var visibleIds = _accessService.VisibleCustomers(scope).Select(x => x.CustomerId);
        var tasks = VisibleTasks(scope.CompanyId, visibleIds).Where(x =>
            x.AssignedToEmployeeId == scope.EmployeeId || x.Assignees.Any(a => a.EmployeeId == scope.EmployeeId && a.IsActive));
        var today = _dateTimeProvider.Now.Date;
        var month = new DateTime(today.Year, today.Month, 1);
        var interactions = _dbContext.CustomerInteractions.AsNoTracking().Where(x => x.CompanyId == scope.CompanyId && x.IsActive &&
            x.AssignedSaleEmployeeId == scope.EmployeeId && visibleIds.Contains(x.CustomerId));
        var dto = new SaleCrmDashboardDto
        {
            TodayTasks = await tasks.CountAsync(x => x.DueDate.HasValue && x.DueDate.Value.Date == today && x.Status != WorkTaskStatus.Done && x.Status != WorkTaskStatus.Canceled, cancellationToken),
            OverdueTasks = await tasks.CountAsync(x => x.DueDate.HasValue && x.DueDate.Value.Date < today && x.Status != WorkTaskStatus.Done && x.Status != WorkTaskStatus.Canceled, cancellationToken),
            PendingTasks = await tasks.CountAsync(x => x.Status == WorkTaskStatus.Pending || x.Status == WorkTaskStatus.InProgress, cancellationToken),
            CompletedTasksThisMonth = await tasks.CountAsync(x => x.Status == WorkTaskStatus.Done && x.CompletedDate >= month, cancellationToken),
            InteractionsThisMonth = await interactions.CountAsync(x => x.InteractionAt >= month, cancellationToken),
            NextFollowUpDate = await tasks.Where(x => x.IsActive && x.DueDate.HasValue && x.Status != WorkTaskStatus.Done && x.Status != WorkTaskStatus.Canceled).MinAsync(x => x.DueDate, cancellationToken)
        };
        return OperationResult<SaleCrmDashboardDto>.Ok(dto);
    }

    /// <summary>
    /// Trả dashboard leader trên toàn bộ khách hàng và nhân viên thuộc visibility scope của leader.
    /// </summary>
    public Task<OperationResult<TeamCrmDashboardDto>> GetLeaderDashboardAsync(CancellationToken cancellationToken = default)
        => GetTeamDashboardAsync(cancellationToken);

    /// <summary>
    /// Trả dashboard tổng quan dành cho director/admin; HTTP endpoint còn kiểm tra role quản lý.
    /// </summary>
    public Task<OperationResult<TeamCrmDashboardDto>> GetDirectorDashboardAsync(CancellationToken cancellationToken = default)
        => GetTeamDashboardAsync(cancellationToken);

    private async Task<OperationResult<TeamCrmDashboardDto>> GetTeamDashboardAsync(CancellationToken cancellationToken)
    {
        var scope = await _accessService.BuildScopeAsync(cancellationToken);
        var visibleCustomers = _accessService.VisibleCustomers(scope);
        var visibleIds = visibleCustomers.Select(x => x.CustomerId);
        var tasks = VisibleTasks(scope.CompanyId, visibleIds);
        var plans = _dbContext.WorkPlans.AsNoTracking().Where(x => x.CompanyId == scope.CompanyId && x.IsActive &&
            x.References.Any(r => r.ReferenceType == WorkReferenceType.Customer && visibleIds.Contains(r.ReferenceId)));
        var interactions = _dbContext.CustomerInteractions.AsNoTracking().Where(x => x.CompanyId == scope.CompanyId && x.IsActive && visibleIds.Contains(x.CustomerId));
        var today = _dateTimeProvider.Now.Date;
        var month = new DateTime(today.Year, today.Month, 1);
        return OperationResult<TeamCrmDashboardDto>.Ok(new TeamCrmDashboardDto
        {
            ActiveCustomers = await visibleCustomers.CountAsync(cancellationToken),
            OpenTasks = await tasks.CountAsync(x => x.Status != WorkTaskStatus.Done && x.Status != WorkTaskStatus.Canceled, cancellationToken),
            OverdueTasks = await tasks.CountAsync(x => x.DueDate.HasValue && x.DueDate.Value.Date < today && x.Status != WorkTaskStatus.Done && x.Status != WorkTaskStatus.Canceled, cancellationToken),
            CompletedTasksThisMonth = await tasks.CountAsync(x => x.Status == WorkTaskStatus.Done && x.CompletedDate >= month, cancellationToken),
            InteractionsThisMonth = await interactions.CountAsync(x => x.InteractionAt >= month, cancellationToken),
            ActiveWorkPlans = await plans.CountAsync(x => x.Status == WorkPlanStatus.Active, cancellationToken)
        });
    }

    /// <summary>
    /// Tổng hợp khách hàng có interaction trong kỳ và bổ sung khách có doanh số khi được yêu cầu.
    /// Task chỉ dùng để thống kê trên các khách đã đủ điều kiện, không làm phát sinh dòng báo cáo riêng.
    /// </summary>
    public async Task<OperationResult<CustomerActivityCalendarReportDto>> GetActivityReportAsync(
        CustomerActivityCalendarReportQuery query,
        CancellationToken cancellationToken = default)
    {
        var scope = await _accessService.BuildScopeAsync(cancellationToken);
        var range = ResolveReportRange(query, _dateTimeProvider.Now);
        var now = scope.Now;
        var leaderGroupIds = scope.LeaderGroupIds.ToArray();
        var restrictAssignmentsToLeaderGroups = !scope.HasFullCustomerView && leaderGroupIds.Length > 0;

        if (!range.IsValid) 
            return OperationResult<CustomerActivityCalendarReportDto>.Fail(range.Error!);

        var employee = await _accessService.ResolveEmployeeAsync(scope, query.AssignedSaleEmployeeId, query.OnlyMine, cancellationToken);

        if (!employee.IsAllowed) 
            return OperationResult<CustomerActivityCalendarReportDto>.Fail("Employee is outside your scope.");

        var group = await ResolveGroupMemberEmployeeIdsAsync(scope, query.GroupId, cancellationToken);
        if (!group.IsAllowed)
            return OperationResult<CustomerActivityCalendarReportDto>.Fail("Group is outside your scope.");

        var customerQuery = _accessService.VisibleCustomers(scope);

        if (query.CustomerId.HasValue) 
            customerQuery = customerQuery.Where(x => x.CustomerId == query.CustomerId);

        if (employee.EmployeeId.HasValue)
        {
            var employeeId = employee.EmployeeId.Value;
            customerQuery = customerQuery.Where(x =>
                x.CurrentSaleId == employeeId ||
                x.CustomerAssignments.Any(a => a.IsActive && a.EmployeeId == employeeId) ||
                x.CustomerClaims.Any(cl =>
                    cl.EmployeeId == employeeId &&
                    cl.IsActive &&
                    cl.Type == ClaimType.Work &&
                cl.ExpiresAt > now));
        }

        if (group.EmployeeIds is { } groupEmployeeIds)
        {
            customerQuery = customerQuery.Where(x =>
                groupEmployeeIds.Contains(x.CurrentSaleId ?? Guid.Empty) ||
                x.CustomerAssignments.Any(a => a.IsActive && groupEmployeeIds.Contains(a.EmployeeId)) ||
                x.CustomerClaims.Any(cl =>
                    groupEmployeeIds.Contains(cl.EmployeeId) &&
                    cl.IsActive &&
                    cl.Type == ClaimType.Work &&
                    cl.ExpiresAt > now));
        }

        if (query.NormalizedKeyword is { } keyword)
        {
            customerQuery = customerQuery.Where(x => x.ExternalId.Contains(keyword) || x.CustomerName.Contains(keyword));
        }

        var customers = await customerQuery
            .Select(x => new
            {
                x.CustomerId,
                x.ExternalId,
                x.CustomerName,
                x.IsLead,
                x.CurrentSaleId,
                HasActiveAssignment = x.CustomerAssignments.Any(a => a.IsActive),
                LatestAssignment = x.CustomerAssignments
                    .Where(a =>
                        a.IsActive &&
                        (!restrictAssignmentsToLeaderGroups || leaderGroupIds.Contains(a.GroupId)))
                    .OrderByDescending(a => a.CreatedDate)
                    .Select(a => new
                    {
                        EmployeeId = (Guid?)a.EmployeeId,
                        EmployeeName = a.Employee.FullName
                    })
                    .FirstOrDefault(),
                LatestClaim = x.CustomerClaims
                    .Where(cl =>
                        cl.IsActive &&
                        cl.Type == ClaimType.Work &&
                        cl.ExpiresAt > now)
                    .OrderByDescending(cl => cl.ExpiresAt)
                    .Select(cl => new
                    {
                        EmployeeId = (Guid?)cl.EmployeeId,
                        EmployeeName = cl.Employee.FullName
                    })
                    .FirstOrDefault(),
                CurrentSaleName = _dbContext.Employees
                    .Where(currentSale =>
                        x.CurrentSaleId.HasValue &&
                        currentSale.EmployeeId == x.CurrentSaleId.Value)
                    .Select(currentSale => currentSale.FullName)
                    .FirstOrDefault()
            })
            .Select(x => new
            {
                x.CustomerId,
                x.ExternalId,
                x.CustomerName,
                AssignedSaleEmployeeId = x.IsLead && !x.HasActiveAssignment
                    ? x.LatestClaim != null ? x.LatestClaim.EmployeeId : x.CurrentSaleId
                    : x.LatestAssignment != null ? x.LatestAssignment.EmployeeId : x.CurrentSaleId,
                AssignedSaleEmployeeName = x.IsLead && !x.HasActiveAssignment
                    ? x.LatestClaim != null ? x.LatestClaim.EmployeeName : x.CurrentSaleName
                    : x.LatestAssignment != null ? x.LatestAssignment.EmployeeName : x.CurrentSaleName
            })
            .ToListAsync(cancellationToken);

        var ids = customers.Select(x => x.CustomerId).ToArray();

        var revenues = await DeliveryRevenueQuery.Create(
                _dbContext.DeliveryOrderDetails.AsNoTracking(),
                range.From,
                range.ToExclusive,
                scope.CompanyId)
            .Where(x => ids.Contains(x.CustomerId))
            .GroupBy(x => x.CustomerId).Select(g => new
            {
                CustomerId = g.Key,
                Amount = g.Sum(x => x.RevenueAmountVnd)
            })
            .ToDictionaryAsync(x => x.CustomerId, x => x.Amount, cancellationToken);

        var interactionQuery = _dbContext.CustomerInteractions
            .AsNoTracking()
            .Where(x => x.CompanyId == scope.CompanyId && x.IsActive &&
            ids.Contains(x.CustomerId) && x.InteractionAt >= range.From && x.InteractionAt < range.ToExclusive);

        if (group.EmployeeIds is { } reportGroupEmployeeIds)
        {
            interactionQuery = interactionQuery.Where(x =>
                x.AssignedSaleEmployeeId.HasValue &&
                reportGroupEmployeeIds.Contains(x.AssignedSaleEmployeeId.Value));
        }

        var interactions = await interactionQuery
            .Select(x => new ActivityReportInteraction
            {
                CustomerId = x.CustomerId,
                InteractionType = x.InteractionType,
                InteractionAt = x.InteractionAt
            })
            .ToListAsync(cancellationToken);

        var taskReportQuery = _dbContext.WorkTasks
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == scope.CompanyId && x.IsActive &&
                x.References.Any(r => r.ReferenceType == WorkReferenceType.Customer && ids.Contains(r.ReferenceId)) &&
                ((x.CreatedDate >= range.From && x.CreatedDate < range.ToExclusive) ||
                 (x.CompletedDate >= range.From && x.CompletedDate < range.ToExclusive)));

        if (group.EmployeeIds is { } taskGroupEmployeeIds)
        {
            taskReportQuery = taskReportQuery.Where(x =>
                x.AssignedToEmployeeId.HasValue &&
                taskGroupEmployeeIds.Contains(x.AssignedToEmployeeId.Value));
        }

        var tasks = await taskReportQuery
            .Select(x => new
            {
                CustomerId = x.References.Where(r => r.ReferenceType == WorkReferenceType.Customer && r.IsPrimary).Select(r => r.ReferenceId).First(), x.Status
            })
            .ToListAsync(cancellationToken);

        var healthSummaries = await BuildCustomerHealthSummariesAsync(
            ids,
            scope.CompanyId,
            query,
            range,
            cancellationToken);

        var reportDates = Enumerable.Range(0, (range.ToExclusive - range.From).Days)
            .Select(offset => range.From.AddDays(offset))
            .ToArray();

        var rows = customers.Select(x =>
        {
            var customerInteractions = interactions.Where(i => i.CustomerId == x.CustomerId).ToArray();
            var customerTasks = tasks.Where(t => t.CustomerId == x.CustomerId).ToArray();
            var revenue = revenues.GetValueOrDefault(x.CustomerId);
            healthSummaries.TryGetValue(x.CustomerId, out var health);
            return new CustomerActivityReportRowDto
            {
                CustomerId = x.CustomerId, RevenueGroupCode = ResolveRevenueGroup(revenue),
                CustomerExternalId = x.ExternalId, CustomerName = x.CustomerName,
                AssignedSaleEmployeeId = x.AssignedSaleEmployeeId, AssignedSaleEmployeeName = x.AssignedSaleEmployeeName, RevenueAmount = revenue,
                ActivityCount = customerInteractions.Count(i => i.InteractionType is CustomerInteractionType.Meeting or CustomerInteractionType.Visit),
                ContactCount = customerInteractions.Count(i => i.InteractionType is not (CustomerInteractionType.Meeting or CustomerInteractionType.Visit)),
                CompletedTaskCount = customerTasks.Count(t => t.Status == WorkTaskStatus.Done),
                OpenTaskCount = customerTasks.Count(t => CustomerCrmTaskRules.IsOpen(t.Status)),
                HealthCode = health?.HealthCode ?? CustomerHealthCode.Unknown,
                HealthSummary = health?.HealthSummary,
                CustomerNeed = health?.CustomerNeed,
                CurrentStage = health?.CurrentStage,
                Risk = health?.Risk,
                SuggestedNextAction = health?.SuggestedNextAction,
                HealthGeneratedDate = health?.HealthGeneratedDate,
                DailyContacts = BuildDailyContacts(customerInteractions, reportDates)
            };
        }).Where(x =>
            x.ActivityCount + x.ContactCount > 0 ||
            query.IncludeCustomersWithoutActivity && x.RevenueAmount > 0)
            .ToList();

        var requestedHealthCodes = ResolveRequestedHealthCodes(query);
        if (requestedHealthCodes.Count > 0)
        {
            rows = rows.Where(x => requestedHealthCodes.Contains(x.HealthCode)).ToList();
        }

        var pageNumber = query.NormalizedPageNumber;
        var pageSize = query.NormalizedPageSize;
        var pageRows = rows
            .OrderBy(x => RevenueGroupOrder(x.RevenueGroupCode))
            .ThenByDescending(x => x.RevenueAmount)
            .ThenBy(x => x.CustomerName)
            .ThenBy(x => x.CustomerId)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return OperationResult<CustomerActivityCalendarReportDto>.Ok(new CustomerActivityCalendarReportDto
        {
            Header = new CustomerActivityReportHeaderDto
            {
                From = range.From,
                To = range.ToExclusive.AddTicks(-1),
                TotalRevenueAmount = rows.Sum(x => x.RevenueAmount),
                TotalMeetingVisitCount = rows.Sum(x => x.ActivityCount),
                TotalOtherInteractionCount = rows.Sum(x => x.ContactCount),
                PositiveHealthCount = rows.Count(x => x.HealthCode == CustomerHealthCode.Positive),
                NeutralHealthCount = rows.Count(x => x.HealthCode == CustomerHealthCode.Neutral),
                NegativeHealthCount = rows.Count(x => x.HealthCode == CustomerHealthCode.Negative),
                UnknownHealthCount = rows.Count(x => x.HealthCode == CustomerHealthCode.Unknown)
            },
            Customers = new PagedResult<CustomerActivityReportRowDto>(pageRows, rows.Count, pageNumber, pageSize)
        });
    }

    /// <summary>
    /// Giới hạn WorkTask theo company và Customer reference nằm trong visibility scope.
    /// Điều kiện này bắt buộc để tránh người dùng đoán task id hoặc xem task của khách hàng ngoài quyền.
    /// </summary>
    /// <summary>
    /// Lấy health khách hàng từ AI summary đã có, không gọi AI mới trong lúc dựng report.
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, ActivityReportCustomerHealth>> BuildCustomerHealthSummariesAsync(
        IReadOnlyCollection<Guid> customerIds,
        Guid companyId,
        CustomerActivityCalendarReportQuery query,
        DateRange range,
        CancellationToken cancellationToken)
    {
        if (customerIds.Count == 0)
        {
            return new Dictionary<Guid, ActivityReportCustomerHealth>();
        }

        var candidates = await _dbContext.CustomerInteractionAiSummaries
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == companyId &&
                x.IsActive &&
                customerIds.Contains(x.CustomerId) &&
                (x.IsAiSuccess ||
                 x.Sentiment != string.Empty ||
                 x.Summary != string.Empty ||
                 x.CustomerNeed != string.Empty ||
                 x.CurrentStage != string.Empty ||
                 x.Risk != string.Empty ||
                 x.NextAction != string.Empty))
            .Select(x => new ActivityReportCustomerHealthCandidate
            {
                CustomerId = x.CustomerId,
                SummaryScope = x.SummaryScope,
                Year = x.Year,
                Month = x.Month,
                PeriodFrom = x.PeriodFrom,
                PeriodTo = x.PeriodTo,
                Summary = x.Summary,
                CustomerNeed = x.CustomerNeed,
                CurrentStage = x.CurrentStage,
                Risk = x.Risk,
                NextAction = x.NextAction,
                Sentiment = x.Sentiment,
                AiGeneratedDate = x.AiGeneratedDate,
                UpdatedDate = x.UpdatedDate,
                CreatedDate = x.CreatedDate
            })
            .ToListAsync(cancellationToken);

        return candidates
            .GroupBy(x => x.CustomerId)
            .Select(group => group
                .OrderBy(x => ResolveHealthSummaryRank(x, query, range))
                .ThenByDescending(x => x.AiGeneratedDate ?? x.UpdatedDate ?? x.CreatedDate)
                .First())
            .ToDictionary(
                x => x.CustomerId,
                x => new ActivityReportCustomerHealth(
                    NormalizeHealthCode(x.Sentiment),
                    NormalizeOptionalText(x.Summary),
                    NormalizeOptionalText(x.CustomerNeed),
                    NormalizeOptionalText(x.CurrentStage),
                    NormalizeOptionalText(x.Risk),
                    NormalizeOptionalText(x.NextAction),
                    x.AiGeneratedDate));
    }

    private static int ResolveHealthSummaryRank(
        ActivityReportCustomerHealthCandidate candidate,
        CustomerActivityCalendarReportQuery query,
        DateRange range)
    {
        var hasCustomRange = query.From.HasValue || query.To.HasValue;
        var reportYear = query.Year ?? range.From.Year;

        if (!hasCustomRange &&
            query.Month.HasValue &&
            candidate.SummaryScope == CustomerInteractionSummaryScope.Monthly &&
            candidate.Year == reportYear &&
            candidate.Month == query.Month.Value)
        {
            return 0;
        }

        if (!hasCustomRange &&
            !query.Month.HasValue &&
            candidate.SummaryScope == CustomerInteractionSummaryScope.Yearly &&
            candidate.Year == reportYear)
        {
            return 0;
        }

        if (PeriodsOverlap(candidate.PeriodFrom, candidate.PeriodTo, range.From, range.ToExclusive))
        {
            return 1;
        }

        return 10;
    }

    private static bool PeriodsOverlap(
        DateTime candidateFrom,
        DateTime candidateTo,
        DateTime reportFrom,
        DateTime reportToExclusive)
        => candidateFrom < reportToExclusive && candidateTo > reportFrom;

    private static CustomerHealthCode NormalizeHealthCode(string? sentiment)
        => sentiment?.Trim().ToLowerInvariant() switch
        {
            "positive" or "tốt" or "tot" or "tích cực" or "tich cuc" => CustomerHealthCode.Positive,
            "neutral" or "trung lập" or "trung lap" => CustomerHealthCode.Neutral,
            "negative" or "tiêu cực" or "tieu cuc" or "rủi ro" or "rui ro" => CustomerHealthCode.Negative,
            _ => CustomerHealthCode.Unknown
        };

    private static string? NormalizeOptionalText(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IReadOnlySet<CustomerHealthCode> ResolveRequestedHealthCodes(CustomerActivityCalendarReportQuery query)
        => (query.HealthCodes?.Count > 0 ? query.HealthCodes : query.ReportHealthCodes)?.ToHashSet()
            ?? new HashSet<CustomerHealthCode>();

    private IQueryable<WorkTask> VisibleTasks(Guid companyId, IQueryable<Guid> visibleCustomerIds)
        => _dbContext.WorkTasks.AsNoTracking().Where(x => x.CompanyId == companyId && x.IsActive &&
            x.References.Any(r => r.ReferenceType == WorkReferenceType.Customer && visibleCustomerIds.Contains(r.ReferenceId)));

    /// <summary>
    /// Resolve group lọc calendar theo member active; leader chỉ được chọn group mình quản lý, full-view được chọn group trong company.
    /// </summary>
    private async Task<GroupResolution> ResolveGroupMemberEmployeeIdsAsync(
        ViewerScope scope,
        Guid? requestedGroupId,
        CancellationToken cancellationToken)
    {
        if (!requestedGroupId.HasValue)
        {
            return GroupResolution.Allowed(null);
        }

        if (requestedGroupId.Value == Guid.Empty)
        {
            return GroupResolution.Denied();
        }

        if (!scope.HasFullCustomerView && !scope.LeaderGroupIds.Contains(requestedGroupId.Value))
        {
            return GroupResolution.Denied();
        }

        var groupExists = await _dbContext.Groups
            .AsNoTracking()
            .AnyAsync(x =>
                x.GroupId == requestedGroupId.Value &&
                x.CompanyId == scope.CompanyId,
                cancellationToken);

        if (!groupExists)
        {
            return GroupResolution.Denied();
        }

        var employeeIds = await _dbContext.MemberInGroups
            .AsNoTracking()
            .Where(x =>
                x.GroupId == requestedGroupId.Value &&
                x.IsActive &&
                x.Profile.HasValue &&
                x.ProfileNavigation != null &&
                x.ProfileNavigation.CompanyId == scope.CompanyId &&
                x.ProfileNavigation.IsActive)
            .Select(x => x.Profile!.Value)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        return GroupResolution.Allowed(employeeIds);
    }

    /// <summary>
    /// Chuẩn hóa khoảng ngày thành dạng nửa mở [From, ToExclusive) để query không bỏ sót dữ liệu trong ngày cuối.
    /// Khoảng calendar/report được giới hạn tối đa 366 ngày nhằm tránh truy vấn tổng hợp quá lớn.
    /// </summary>
    private static DateRange ResolveRange(DateTime? from, DateTime? to, DateTime now)
    {
        var start = (from ?? new DateTime(now.Year, now.Month, 1)).Date;
        var end = (to ?? start.AddMonths(1).AddDays(-1)).Date.AddDays(1);
        if (end <= start) return DateRange.Invalid("To date must be on or after from date.");
        if ((end - start).TotalDays > 366) return DateRange.Invalid("Calendar range cannot exceed 366 days.");
        return DateRange.Valid(start, end);
    }

    /// <summary>
    /// Ưu tiên From/To do client truyền; nếu không có thì suy ra khoảng mặc định từ chế độ Day, Week, FourDays, Month hoặc Year.
    /// </summary>
    private static DateRange ResolveCalendarRange(CustomerCrmCalendarQuery query, DateTime now)
    {
        if (query.From.HasValue || query.To.HasValue)
            return ResolveRange(query.From, query.To, now);
        var start = query.View switch
        {
            CustomerCrmCalendarView.Day => now.Date,
            CustomerCrmCalendarView.Week => now.Date.AddDays(-(((int)now.DayOfWeek + 6) % 7)),
            CustomerCrmCalendarView.FourDays => now.Date,
            CustomerCrmCalendarView.Year => new DateTime(now.Year, 1, 1),
            _ => new DateTime(now.Year, now.Month, 1)
        };
        var end = query.View switch
        {
            CustomerCrmCalendarView.Day => start.AddDays(1),
            CustomerCrmCalendarView.Week => start.AddDays(7),
            CustomerCrmCalendarView.FourDays => start.AddDays(4),
            CustomerCrmCalendarView.Year => start.AddYears(1),
            _ => start.AddMonths(1)
        };
        return DateRange.Valid(start, end);
    }

    /// <summary>
    /// Xác định kỳ báo cáo theo From/To hoặc Year/Month và kiểm tra giới hạn năm, tháng hợp lệ.
    /// </summary>
    private static DateRange ResolveReportRange(CustomerActivityCalendarReportQuery query, DateTime now)
    {
        if (query.From.HasValue || query.To.HasValue) return ResolveRange(query.From, query.To, now);
        var year = query.Year ?? now.Year;
        if (year is < 2000 or > 2100) return DateRange.Invalid("Report year is invalid.");
        if (query.Month.HasValue && query.Month is < 1 or > 12) return DateRange.Invalid("Report month is invalid.");
        var start = query.Month.HasValue ? new DateTime(year, query.Month.Value, 1) : new DateTime(year, 1, 1);
        return DateRange.Valid(start, query.Month.HasValue ? start.AddMonths(1) : start.AddYears(1));
    }

    /// <summary>
    /// Phân khách hàng vào mã nhóm doanh thu ổn định; FE tự ánh xạ mã sang label hiển thị.
    /// </summary>
    private static string ResolveRevenueGroup(decimal revenue) => revenue switch
    {
        >= HighRevenueThreshold => CustomerCrmReportGroupCodes.AtLeastOneHundredMillion,
        >= MediumRevenueThreshold => CustomerCrmReportGroupCodes.FiftyToOneHundredMillion,
        > 0 => CustomerCrmReportGroupCodes.BelowFiftyMillion,
        _ => CustomerCrmReportGroupCodes.NoRevenue
    };

    /// <summary>
    /// Xác định thứ tự nhóm doanh thu từ cao xuống thấp khi dựng báo cáo.
    /// </summary>
    private static int RevenueGroupOrder(string code) => code switch
    {
        CustomerCrmReportGroupCodes.AtLeastOneHundredMillion => 1,
        CustomerCrmReportGroupCodes.FiftyToOneHundredMillion => 2,
        CustomerCrmReportGroupCodes.BelowFiftyMillion => 3,
        _ => 4
    };

    /// <summary>
    /// Tạo đầy đủ các ngày trong kỳ để FE vẽ heatmap liên hệ; ngày không có interaction vẫn trả level 0.
    /// </summary>
    private static IReadOnlyList<CustomerActivityReportDailyContactDto> BuildDailyContacts(
        IReadOnlyCollection<ActivityReportInteraction> interactions,
        IReadOnlyList<DateTime> reportDates)
    {
        var countsByDate = interactions
            .GroupBy(x => x.InteractionAt.Date)
            .ToDictionary(
                x => x.Key,
                x => new
                {
                    InteractionCount = x.Count(),
                    MeetingVisitCount = x.Count(i => i.InteractionType is CustomerInteractionType.Meeting or CustomerInteractionType.Visit),
                    OtherInteractionCount = x.Count(i => i.InteractionType is not (CustomerInteractionType.Meeting or CustomerInteractionType.Visit))
                });

        return reportDates.Select(date =>
        {
            countsByDate.TryGetValue(date.Date, out var counts);
            var interactionCount = counts?.InteractionCount ?? 0;

            return new CustomerActivityReportDailyContactDto
            {
                Date = date,
                Day = date.Day,
                InteractionCount = interactionCount,
                MeetingVisitCount = counts?.MeetingVisitCount ?? 0,
                OtherInteractionCount = counts?.OtherInteractionCount ?? 0,
                Level = ResolveDailyContactLevel(interactionCount)
            };
        }).ToList();
    }

    private static int ResolveDailyContactLevel(int interactionCount) => interactionCount switch
    {
        <= 0 => 0,
        1 => 1,
        2 => 2,
        <= 4 => 3,
        _ => 4
    };

    /// <summary>
    /// Chuẩn hóa CalendarEvent
    /// </summary>
    /// <param name="sourceType"></param>
    /// <param name="sourceId"></param>
    /// <returns></returns>
    private static string BuildCalendarEventId(CustomerCrmCalendarSourceType sourceType, Guid sourceId)
    => $"{sourceType}:{sourceId}";

    /// <summary>
    /// Kết quả resolve khoảng ngày, trong đó ToExclusive không thuộc khoảng truy vấn.
    /// </summary>
    private sealed record DateRange(DateTime From, DateTime ToExclusive, bool IsValid, string? Error)
    {
        public static DateRange Valid(DateTime from, DateTime toExclusive) => new(from, toExclusive, true, null);
        public static DateRange Invalid(string error) => new(default, default, false, error);
    }

    private sealed record GroupResolution(bool IsAllowed, Guid[]? EmployeeIds)
    {
        public static GroupResolution Allowed(Guid[]? employeeIds) => new(true, employeeIds);
        public static GroupResolution Denied() => new(false, null);
    }

    private sealed record ActivityReportCustomerHealth(
        CustomerHealthCode HealthCode,
        string? HealthSummary,
        string? CustomerNeed,
        string? CurrentStage,
        string? Risk,
        string? SuggestedNextAction,
        DateTime? HealthGeneratedDate);

    private sealed class ActivityReportCustomerHealthCandidate
    {
        public Guid CustomerId { get; init; }
        public CustomerInteractionSummaryScope SummaryScope { get; init; }
        public int? Year { get; init; }
        public int? Month { get; init; }
        public DateTime PeriodFrom { get; init; }
        public DateTime PeriodTo { get; init; }
        public string Summary { get; init; } = string.Empty;
        public string CustomerNeed { get; init; } = string.Empty;
        public string CurrentStage { get; init; } = string.Empty;
        public string Risk { get; init; } = string.Empty;
        public string NextAction { get; init; } = string.Empty;
        public string Sentiment { get; init; } = string.Empty;
        public DateTime? AiGeneratedDate { get; init; }
        public DateTime? UpdatedDate { get; init; }
        public DateTime CreatedDate { get; init; }
    }

    private sealed class ActivityReportInteraction
    {
        public Guid CustomerId { get; init; }
        public CustomerInteractionType InteractionType { get; init; }
        public DateTime InteractionAt { get; init; }
    }

}
