using System.Text.Json;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Commons.Patching;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using HRM.Application.Features.Notifications.Dtos;
using HRM.Application.Features.Notifications.Services;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.WorkTaskSchema;
using HRM.Domain.Enums.Notifications;
using HRM.Domain.Enums.WorkTaskEnums;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.CustomerCare.Services;

/// <summary>
/// Quản lý follow-up task, task assignee và work plan CRM trên các entity dùng chung trong WorkTaskSchema.
/// </summary>
internal sealed class CustomerCrmWorkService : ICustomerCrmWorkService
{
    private readonly ICRMReadDbContext _readDbContext;
    private readonly ICRMWriteDbContext _writeDbContext;
    private readonly CustomerCrmAccessService _accessService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly INotificationService _notificationService;

    public CustomerCrmWorkService(
        ICRMReadDbContext readDbContext,
        ICRMWriteDbContext writeDbContext,
        CustomerCrmAccessService accessService,
        IDateTimeProvider dateTimeProvider,
        INotificationService notificationService)
    {
        _readDbContext = readDbContext;
        _writeDbContext = writeDbContext;
        _accessService = accessService;
        _dateTimeProvider = dateTimeProvider;
        _notificationService = notificationService;
    }

    /// <summary>
    /// Lấy task mà current employee là người phụ trách chính hoặc assignee hỗ trợ.
    /// </summary>
    public async Task<OperationResult<PagedResult<CustomerFollowUpTaskDto>>> GetMyTasksAsync(
        CustomerFollowUpTaskQuery query,
        CancellationToken cancellationToken = default)
    {
        var scope = await _accessService.BuildScopeAsync(cancellationToken);
        var source = BuildVisibleTaskQuery(scope)
            .Where(x => x.AssignedToEmployeeId == scope.EmployeeId ||
                        x.Assignees.Any(a => a.EmployeeId == scope.EmployeeId && a.IsActive));
        return OperationResult<PagedResult<CustomerFollowUpTaskDto>>.Ok(
            await PageTasksAsync(source, query, cancellationToken));
    }

    /// <summary>
    /// Lấy chi tiết WorkTask khi Customer reference của task nằm trong visibility scope.
    /// </summary>
    public async Task<OperationResult<CustomerFollowUpTaskDto>> GetTaskAsync(
        Guid taskId,
        CancellationToken cancellationToken = default)
    {
        var scope = await _accessService.BuildScopeAsync(cancellationToken);
        var task = await ProjectTasks(BuildVisibleTaskQuery(scope), _dateTimeProvider.Now.Date)
            .FirstOrDefaultAsync(x => x.WorkTaskId == taskId, cancellationToken);
        return task is null
            ? OperationResult<CustomerFollowUpTaskDto>.Fail("Task was not found.")
            : OperationResult<CustomerFollowUpTaskDto>.Ok(task);
    }

    /// <summary>
    /// Lấy danh sách WorkTask follow-up của một customer với filter và phân trang.
    /// </summary>
    public async Task<OperationResult<PagedResult<CustomerFollowUpTaskDto>>> GetCustomerTasksAsync(
        Guid customerId,
        CustomerFollowUpTaskQuery query,
        CancellationToken cancellationToken = default)
    {
        var scope = await _accessService.BuildScopeAsync(cancellationToken);
        if (!await _accessService.VisibleCustomers(scope).AnyAsync(x => x.CustomerId == customerId, cancellationToken))
        {
            return OperationResult<PagedResult<CustomerFollowUpTaskDto>>.Fail("Customer was not found.");
        }
        var interactionIds = _readDbContext.CustomerInteractions
            .AsNoTracking()
            .Where(interaction =>
                interaction.CompanyId == scope.CompanyId &&
                interaction.CustomerId == customerId)
            .Select(interaction => interaction.Id);

        var source = BuildVisibleTaskQuery(scope)
            .Where(task =>
                task.References.Any(reference =>
                    (reference.ReferenceType == WorkReferenceType.Customer &&
                     reference.ReferenceId == customerId) ||
                    (reference.ReferenceType == WorkReferenceType.CustomerInteraction &&
                     interactionIds.Contains(reference.ReferenceId))));
        return OperationResult<PagedResult<CustomerFollowUpTaskDto>>.Ok(
            await PageTasksAsync(source, query, cancellationToken));
    }

    /// <summary>
    /// Lấy các assignee active của WorkTask sau khi kiểm tra customer visibility.
    /// </summary>
    public async Task<OperationResult<IReadOnlyList<CustomerFollowUpTaskAssigneeDto>>> GetTaskAssigneesAsync(
        Guid taskId,
        CancellationToken cancellationToken = default)
    {
        var scope = await _accessService.BuildScopeAsync(cancellationToken);
        if (!await BuildVisibleTaskQuery(scope).AnyAsync(x => x.Id == taskId, cancellationToken))
        {
            return OperationResult<IReadOnlyList<CustomerFollowUpTaskAssigneeDto>>.Fail("Task was not found.");
        }

        return OperationResult<IReadOnlyList<CustomerFollowUpTaskAssigneeDto>>.Ok(
            await ProjectAssignees(taskId).ToListAsync(cancellationToken));
    }

    /// <summary>
    /// Lấy danh sách WorkPlan của customer với filter và phân trang.
    /// </summary>
    public async Task<OperationResult<PagedResult<CustomerWorkPlanDto>>> GetCustomerPlansAsync(
        Guid customerId,
        CustomerWorkPlanQuery query,
        CancellationToken cancellationToken = default)
    {
        var scope = await _accessService.BuildScopeAsync(cancellationToken);
        if (!await _accessService.VisibleCustomers(scope).AnyAsync(x => x.CustomerId == customerId, cancellationToken))
            return OperationResult<PagedResult<CustomerWorkPlanDto>>.Fail("Customer was not found.");
        var source = _readDbContext.WorkPlans.AsNoTracking().Where(x =>
            x.CompanyId == scope.CompanyId &&
            x.References.Any(r => r.ReferenceType == WorkReferenceType.Customer && r.ReferenceId == customerId));
        if (!query.IncludeInactive) source = source.Where(x => x.IsActive);
        if (query.AssignedSaleEmployeeId.HasValue) source = source.Where(x => x.AssignedToEmployeeId == query.AssignedSaleEmployeeId);
        if (query.Status.HasValue) source = source.Where(x => x.Status == query.Status);
        if (query.Priority.HasValue) source = source.Where(x => x.Priority == query.Priority);
        if (!string.IsNullOrWhiteSpace(query.NormalizedKeyword))
        {
            var keyword = query.NormalizedKeyword;
            source = source.Where(x => x.PlanName.Contains(keyword) || (x.Objective ?? string.Empty).Contains(keyword));
        }
        var total = await source.CountAsync(cancellationToken);
        var items = await ProjectPlans(source).OrderBy(x => x.NextFollowUpDate == null).ThenBy(x => x.NextFollowUpDate)
            .ThenByDescending(x => x.WorkPlanId).Skip((query.NormalizedPageNumber - 1) * query.NormalizedPageSize)
            .Take(query.NormalizedPageSize).ToListAsync(cancellationToken);
        return OperationResult<PagedResult<CustomerWorkPlanDto>>.Ok(
            new PagedResult<CustomerWorkPlanDto>(items, total, query.NormalizedPageNumber, query.NormalizedPageSize));
    }

    /// <summary>
    /// Lấy chi tiết WorkPlan khi customer liên kết nằm trong visibility scope.
    /// </summary>
    public async Task<OperationResult<CustomerWorkPlanDto>> GetPlanAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        var scope = await _accessService.BuildScopeAsync(cancellationToken);
        var visibleIds = _accessService.VisibleCustomers(scope).Select(x => x.CustomerId);
        var plan = await ProjectPlans(_readDbContext.WorkPlans.AsNoTracking().Where(x =>
            x.Id == planId && x.CompanyId == scope.CompanyId &&
            x.References.Any(r => r.ReferenceType == WorkReferenceType.Customer && visibleIds.Contains(r.ReferenceId))))
            .FirstOrDefaultAsync(cancellationToken);
        return plan is null
            ? OperationResult<CustomerWorkPlanDto>.Fail("Work plan was not found.")
            : OperationResult<CustomerWorkPlanDto>.Ok(plan);
    }

    /// <summary>
    /// Tạo query danh sách WorkTask mà user hiện tại được phép nhìn thấy.
    /// </summary>
    /// <param name="scope"></param>
    /// <returns></returns>
    private IQueryable<WorkTask> BuildVisibleTaskQuery(ViewerScope scope)
    {
        var visibleCustomerIds = _accessService.VisibleCustomers(scope).Select(x => x.CustomerId);

        var visibleInteractionIds = _readDbContext.CustomerInteractions
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == scope.CompanyId &&
                visibleCustomerIds.Contains(x.CustomerId))
            .Select(x => x.Id);

        return _readDbContext.WorkTasks.AsNoTracking().Where(x =>
            x.CompanyId == scope.CompanyId &&
            x.References.Any(r =>
                (r.ReferenceType == WorkReferenceType.Customer &&
                 visibleCustomerIds.Contains(r.ReferenceId)) ||
                (r.ReferenceType == WorkReferenceType.CustomerInteraction &&
                 visibleInteractionIds.Contains(r.ReferenceId))));
    }

    private async Task<WorkTask?> GetVisibleTaskForUpdateAsync(Guid taskId, ViewerScope scope, CancellationToken cancellationToken)
    {
        var visibleCustomerIds = _accessService.VisibleCustomers(scope).Select(x => x.CustomerId);

        var visibleInteractionIds = _readDbContext.CustomerInteractions
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == scope.CompanyId &&
                visibleCustomerIds.Contains(x.CustomerId))
            .Select(x => x.Id);

        var isVisible = await _writeDbContext.WorkTasks
            .AsNoTracking()
            .AnyAsync(x =>
            x.Id == taskId && x.CompanyId == scope.CompanyId &&
            x.References.Any(r =>
                (r.ReferenceType == WorkReferenceType.Customer &&
                 visibleCustomerIds.Contains(r.ReferenceId)) ||
                (r.ReferenceType == WorkReferenceType.CustomerInteraction &&
                 visibleInteractionIds.Contains(r.ReferenceId))), cancellationToken);

        if (!isVisible)
        {
            return null;
        }

        var trackedTask = _writeDbContext.WorkTasks.Local.FirstOrDefault(x => x.Id == taskId);
        if (trackedTask is not null)
        {
            return trackedTask;
        }

        return await _writeDbContext.WorkTasks
            .FirstOrDefaultAsync(x => x.Id == taskId && x.CompanyId == scope.CompanyId, cancellationToken);
    }

    private async Task<PagedResult<CustomerFollowUpTaskDto>> PageTasksAsync(IQueryable<WorkTask> source, CustomerFollowUpTaskQuery query, CancellationToken cancellationToken)
    {
        var today = _dateTimeProvider.Now.Date;
        if (!query.IncludeInactive) source = source.Where(x => x.IsActive);
        if (query.AssignedSaleEmployeeId.HasValue) source = source.Where(x => x.AssignedToEmployeeId == query.AssignedSaleEmployeeId);
        if (query.Status.HasValue) source = source.Where(x => x.Status == query.Status);
        if (query.Priority.HasValue) source = source.Where(x => x.Priority == query.Priority);
        if (query.OnlyOverdue) source = source.Where(x => x.DueDate.HasValue && x.DueDate.Value.Date < today && x.Status != WorkTaskStatus.Done && x.Status != WorkTaskStatus.Canceled);
        if (query.DueFrom.HasValue) source = source.Where(x => x.DueDate >= query.DueFrom.Value.Date);
        if (query.DueTo.HasValue) source = source.Where(x => x.DueDate < query.DueTo.Value.Date.AddDays(1));
        if (!string.IsNullOrWhiteSpace(query.NormalizedKeyword))
        {
            var keyword = query.NormalizedKeyword;
            source = source.Where(x => x.Title.Contains(keyword) || (x.Description ?? string.Empty).Contains(keyword));
        }
        var total = await source.CountAsync(cancellationToken);
        var items = await ProjectTasks(source, today)
            .OrderByDescending(x => x.IsOverdue).ThenBy(x => x.DueDate == null).ThenBy(x => x.DueDate)
            .ThenByDescending(x => x.WorkTaskId).Skip((query.NormalizedPageNumber - 1) * query.NormalizedPageSize)
            .Take(query.NormalizedPageSize).ToListAsync(cancellationToken);
        return new PagedResult<CustomerFollowUpTaskDto>(items, total, query.NormalizedPageNumber, query.NormalizedPageSize);
    }

    private IQueryable<CustomerFollowUpTaskDto> ProjectTasks(IQueryable<WorkTask> source, DateTime today)
        => source.Select(x => new CustomerFollowUpTaskDto
        {
            WorkTaskId = x.Id,
            CustomerId = x.References.Where(r => r.ReferenceType == WorkReferenceType.Customer)
                .OrderByDescending(r => r.IsPrimary)
                .Select(r => (Guid?)r.ReferenceId)
                .FirstOrDefault() ??
                _readDbContext.CustomerInteractions
                    .Where(interaction =>
                        interaction.CompanyId == x.CompanyId &&
                        x.References.Any(r =>
                            r.ReferenceType == WorkReferenceType.CustomerInteraction &&
                            r.ReferenceId == interaction.Id))
                    .Select(interaction => (Guid?)interaction.CustomerId)
                    .FirstOrDefault() ?? Guid.Empty,
            CustomerExternalId = x.References.Where(r => r.ReferenceType == WorkReferenceType.Customer)
                .OrderByDescending(r => r.IsPrimary)
                .Select(r => r.ReferenceCodeSnapshot)
                .FirstOrDefault() ??
                _readDbContext.CustomerInteractions
                    .Where(interaction =>
                        interaction.CompanyId == x.CompanyId &&
                        x.References.Any(r =>
                            r.ReferenceType == WorkReferenceType.CustomerInteraction &&
                            r.ReferenceId == interaction.Id))
                    .Select(interaction => interaction.Customer.ExternalId)
                    .FirstOrDefault() ?? string.Empty,
            CustomerName = x.References.Where(r => r.ReferenceType == WorkReferenceType.Customer)
                .OrderByDescending(r => r.IsPrimary)
                .Select(r => r.ReferenceNameSnapshot)
                .FirstOrDefault() ??
                _readDbContext.CustomerInteractions
                    .Where(interaction =>
                        interaction.CompanyId == x.CompanyId &&
                        x.References.Any(r =>
                            r.ReferenceType == WorkReferenceType.CustomerInteraction &&
                            r.ReferenceId == interaction.Id))
                    .Select(interaction => interaction.Customer.CustomerName)
                    .FirstOrDefault() ?? string.Empty,
            CustomerInteractionId = x.References.Where(r => r.ReferenceType == WorkReferenceType.CustomerInteraction).Select(r => (Guid?)r.ReferenceId).FirstOrDefault(),
            Title = x.Title, Description = x.Description, NextAction = x.NextAction, Status = x.Status, Priority = x.Priority,
            DueDate = x.DueDate,
            IsOverdue = x.DueDate.HasValue && x.DueDate.Value.Date < today && x.Status != WorkTaskStatus.Done && x.Status != WorkTaskStatus.Canceled,
            AssignedSaleEmployeeId = x.AssignedToEmployeeId,
            AssignedSaleEmployeeName = x.AssignedToEmployee != null ? x.AssignedToEmployee.FullName : null,
            CompletedDate = x.CompletedDate, CompletionNote = x.CompletionNote, IsActive = x.IsActive,
            Assignees = x.Assignees.Where(a => a.IsActive).OrderByDescending(a => a.IsPrimary).Select(a => new CustomerFollowUpTaskAssigneeDto
            {
                AssigneeId = a.Id, EmployeeId = a.EmployeeId, EmployeeExternalId = a.Employee.ExternalId,
                EmployeeName = a.Employee.FullName, IsPrimary = a.IsPrimary
            }).ToList()
        });

    private IQueryable<CustomerFollowUpTaskAssigneeDto> ProjectAssignees(Guid taskId)
        => _readDbContext.WorkTaskAssignees.AsNoTracking()
            .Where(x => x.WorkTaskId == taskId && x.IsActive)
            .OrderByDescending(x => x.IsPrimary)
            .ThenBy(x => x.Employee.FullName)
            .Select(x => new CustomerFollowUpTaskAssigneeDto
            {
                AssigneeId = x.Id,
                EmployeeId = x.EmployeeId,
                EmployeeExternalId = x.Employee.ExternalId,
                EmployeeName = x.Employee.FullName,
                IsPrimary = x.IsPrimary
            });

    private IQueryable<CustomerWorkPlanDto> ProjectPlans(IQueryable<WorkPlan> source)
        => source.Select(x => new CustomerWorkPlanDto
        {
            WorkPlanId = x.Id,
            CustomerId = x.References.Where(r => r.ReferenceType == WorkReferenceType.Customer)
                .OrderByDescending(r => r.IsPrimary)
                .Select(r => r.ReferenceId)
                .FirstOrDefault(),
            CustomerExternalId = x.References.Where(r => r.ReferenceType == WorkReferenceType.Customer)
                .OrderByDescending(r => r.IsPrimary)
                .Select(r => r.ReferenceCodeSnapshot)
                .FirstOrDefault() ?? string.Empty,
            CustomerName = x.References.Where(r => r.ReferenceType == WorkReferenceType.Customer)
                .OrderByDescending(r => r.IsPrimary)
                .Select(r => r.ReferenceNameSnapshot)
                .FirstOrDefault() ?? string.Empty,
            PlanName = x.PlanName,
            Objective = x.Objective,
            Strategy = x.Strategy,
            DiscussionSummary = x.DiscussionSummary,
            NextAction = x.NextAction,
            Status = x.Status,
            Priority = x.Priority,
            StartDate = x.StartDate,
            EndDate = x.EndDate,
            NextFollowUpDate = x.NextFollowUpDate,
            AssignedSaleEmployeeId = x.AssignedToEmployeeId,
            AssignedSaleEmployeeName = x.AssignedToEmployee != null ? x.AssignedToEmployee.FullName : null,
            IsActive = x.IsActive
        });

    private async Task<Customer?> GetVisibleCustomerForUpdateAsync(Guid customerId, ViewerScope scope, CancellationToken cancellationToken)
    {
        var visibleIds = _accessService.VisibleCustomers(scope).Select(x => x.CustomerId);
        return await _writeDbContext.Customers.FirstOrDefaultAsync(x =>
            x.CustomerId == customerId &&
            x.CompanyId == scope.CompanyId &&
            x.IsActive == true &&
            visibleIds.Contains(x.CustomerId), cancellationToken);
    }

    private Task<bool> InteractionBelongsToCustomerAsync(Guid interactionId, Guid customerId, Guid companyId, CancellationToken cancellationToken)
        => _readDbContext.CustomerInteractions.AsNoTracking()
            .AnyAsync(x => x.Id == interactionId && x.CustomerId == customerId && x.CompanyId == companyId, cancellationToken);

    private async Task AddTaskReferencesAsync(Guid taskId, Customer customer, Guid? interactionId, CancellationToken cancellationToken)
    {
        await _writeDbContext.WorkTaskReferences.AddAsync(new WorkTaskReference
        {
            Id = Guid.CreateVersion7(),
            WorkTaskId = taskId,
            ReferenceType = WorkReferenceType.Customer,
            ReferenceId = customer.CustomerId,
            ReferenceCodeSnapshot = customer.ExternalId,
            ReferenceNameSnapshot = customer.CustomerName,
            IsPrimary = true
        }, cancellationToken);

        if (interactionId.HasValue)
        {
            await _writeDbContext.WorkTaskReferences.AddAsync(new WorkTaskReference
            {
                Id = Guid.CreateVersion7(),
                WorkTaskId = taskId,
                ReferenceType = WorkReferenceType.CustomerInteraction,
                ReferenceId = interactionId.Value,
                IsPrimary = false
            }, cancellationToken);
        }
    }

    private async Task<WorkTaskAssignee> UpsertTaskAssigneeAsync(WorkTask task, Guid employeeId, bool isPrimary, Guid actorId, CancellationToken cancellationToken)
    {
        if (isPrimary)
        {
            var currentPrimary = await _writeDbContext.WorkTaskAssignees
                .Where(x => x.WorkTaskId == task.Id && x.IsActive && x.IsPrimary)
                .ToListAsync(cancellationToken);
            foreach (var item in currentPrimary)
            {
                item.IsPrimary = false;
            }

            task.AssignedToEmployeeId = employeeId;
        }

        var assignee = await _writeDbContext.WorkTaskAssignees
            .OrderByDescending(x => x.IsActive)
            .FirstOrDefaultAsync(x => x.WorkTaskId == task.Id && x.EmployeeId == employeeId, cancellationToken);

        if (assignee is null)
        {
            assignee = new WorkTaskAssignee
            {
                Id = Guid.CreateVersion7(),
                WorkTaskId = task.Id,
                EmployeeId = employeeId,
                IsPrimary = isPrimary,
                IsActive = true,
                CreatedDate = _dateTimeProvider.Now,
                CreatedBy = actorId
            };
            await _writeDbContext.WorkTaskAssignees.AddAsync(assignee, cancellationToken);
        }
        else
        {
            assignee.IsActive = true;
            assignee.IsPrimary = isPrimary;
        }

        return assignee;
    }

    private async Task EnsurePrimaryTaskAssigneeAsync(Guid taskId, Guid employeeId, Guid actorId, DateTime now, CancellationToken cancellationToken)
    {
        var task = _writeDbContext.WorkTasks.Local.FirstOrDefault(x => x.Id == taskId)
                   ?? await _writeDbContext.WorkTasks.FirstAsync(x => x.Id == taskId, cancellationToken);
        await UpsertTaskAssigneeAsync(task, employeeId, true, actorId, cancellationToken);
    }

    private async Task PublishAssigneeNotificationAsync(WorkTask task, Guid employeeId, ViewerScope scope, CancellationToken cancellationToken)
    {
        if (employeeId == scope.EmployeeId) return;
        var payload = JsonSerializer.Serialize(new { contentType = "CustomerFollowUpTask", workTaskId = task.Id });
        await _notificationService.PublishAsync(new PublishNotificationRequest
        {
            CompanyId = scope.CompanyId, CreatedBy = scope.EmployeeId,
            Topic = TopicNotifications.CustomerFollowUpTaskAssigneeAdded,
            Severity = task.Priority == WorkTaskPriority.Urgent ? NotificationSeverity.Warning : NotificationSeverity.Info,
            Title = task.Title, Message = task.NextAction ?? task.Description ?? task.Title,
            Link = $"/crm/follow-up-tasks/{task.Id}", PayloadJson = payload,
            TargetUserIds = new[] { employeeId }
        }, cancellationToken);
    }

    private async Task<Customer?> GetTaskCustomerForUpdateAsync(Guid taskId, Guid companyId, CancellationToken cancellationToken)
    {
        var customerId = await _readDbContext.WorkTaskReferences.AsNoTracking()
            .Where(x =>
                x.WorkTaskId == taskId &&
                x.ReferenceType == WorkReferenceType.Customer)
            .OrderByDescending(x => x.IsPrimary)
            .Select(x => (Guid?)x.ReferenceId)
            .FirstOrDefaultAsync(cancellationToken);

        if (!customerId.HasValue)
        {
            customerId = await _readDbContext.WorkTaskReferences.AsNoTracking()
                .Where(x =>
                    x.WorkTaskId == taskId &&
                    x.ReferenceType == WorkReferenceType.CustomerInteraction)
                .Join(
                    _readDbContext.CustomerInteractions.AsNoTracking().Where(interaction => interaction.CompanyId == companyId),
                    reference => reference.ReferenceId,
                    interaction => interaction.Id,
                    (_, interaction) => (Guid?)interaction.CustomerId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return customerId.HasValue ? await _writeDbContext.Customers.FirstOrDefaultAsync(x => x.CustomerId == customerId && x.CompanyId == companyId, cancellationToken) : null;
    }

    private async Task<DateTime?> ResolveNextFollowUpAsync(Guid customerId, Guid companyId, Guid? excludedTaskId, DateTime? candidate, CancellationToken cancellationToken)
    {
        var interactionIds = _readDbContext.CustomerInteractions
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.CustomerId == customerId)
            .Select(x => x.Id);

        var query = _readDbContext.WorkTasks.AsNoTracking().Where(x => x.CompanyId == companyId && x.IsActive && x.DueDate.HasValue &&
            x.Status != WorkTaskStatus.Done && x.Status != WorkTaskStatus.Canceled &&
            x.References.Any(r =>
                (r.ReferenceType == WorkReferenceType.Customer && r.ReferenceId == customerId) ||
                (r.ReferenceType == WorkReferenceType.CustomerInteraction && interactionIds.Contains(r.ReferenceId))));
        if (excludedTaskId.HasValue) query = query.Where(x => x.Id != excludedTaskId.Value);
        var existing = await query.MinAsync(x => x.DueDate, cancellationToken);
        if (!candidate.HasValue) return existing;
        return !existing.HasValue || candidate < existing ? candidate : existing;
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
