using System.Text.Json;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using HRM.Application.Features.CRM.CustomerCare.Services;
using HRM.Application.Features.Notifications.Dtos;
using HRM.Application.Features.Notifications.Services;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.WorkTaskSchema;
using HRM.Domain.Enums.Notifications;
using HRM.Domain.Enums.WorkTaskEnums;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.CustomerCare.Commands.CustomerFollowUpTasks;

/// <summary>
/// Helper ghi dữ liệu cho command follow-up task CRM; đặt gần handler để không làm phình CustomerCrmWorkService.
/// </summary>
internal sealed class CustomerFollowUpTaskCommandSupport
{
    private readonly ICRMReadDbContext _readDbContext;
    private readonly ICRMWriteDbContext _writeDbContext;
    private readonly CustomerCrmAccessService _accessService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly INotificationService _notificationService;

    public CustomerFollowUpTaskCommandSupport(
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

    public ICRMWriteDbContext WriteDbContext => _writeDbContext;
    public ICRMReadDbContext ReadDbContext => _readDbContext;
    public CustomerCrmAccessService AccessService => _accessService;
    public IDateTimeProvider DateTimeProvider => _dateTimeProvider;

    public async Task<Customer?> GetVisibleCustomerForUpdateAsync(Guid customerId, ViewerScope scope, CancellationToken cancellationToken)
    {
        var visibleIds = _accessService.VisibleCustomers(scope).Select(x => x.CustomerId);
        return await _writeDbContext.Customers
            .AsTracking()
            .FirstOrDefaultAsync(x =>
                x.CustomerId == customerId &&
                x.CompanyId == scope.CompanyId &&
                x.IsActive == true &&
                visibleIds.Contains(x.CustomerId), cancellationToken);
    }

    public Task<bool> InteractionBelongsToCustomerAsync(Guid interactionId, Guid customerId, Guid companyId, CancellationToken cancellationToken)
        => _readDbContext.CustomerInteractions.AsNoTracking()
            .AnyAsync(x => x.Id == interactionId && x.CustomerId == customerId && x.CompanyId == companyId, cancellationToken);

    public async Task AddTaskReferencesAsync(Guid taskId, Customer customer, Guid? interactionId, CancellationToken cancellationToken)
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

    public async Task<WorkTask?> GetVisibleTaskForUpdateAsync(Guid taskId, ViewerScope scope, CancellationToken cancellationToken)
    {
        var visibleCustomerIds = _accessService.VisibleCustomers(scope).Select(x => x.CustomerId);

        var visibleInteractionIds = _readDbContext.CustomerInteractions
            .AsNoTracking()
            .Where(x => x.CompanyId == scope.CompanyId && visibleCustomerIds.Contains(x.CustomerId))
            .Select(x => x.Id);

        var isVisible = await _writeDbContext.WorkTasks
            .AsNoTracking()
            .AnyAsync(x =>
                x.Id == taskId &&
                x.CompanyId == scope.CompanyId &&
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
            .AsTracking()
            .FirstOrDefaultAsync(x =>
                x.Id == taskId &&
                x.CompanyId == scope.CompanyId,
                cancellationToken);
    }

    public async Task<WorkTaskAssignee> UpsertTaskAssigneeAsync(
        WorkTask task,
        Guid employeeId,
        bool isPrimary,
        Guid actorId,
        CancellationToken cancellationToken)
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

    public async Task EnsurePrimaryTaskAssigneeAsync(Guid taskId, Guid employeeId, Guid actorId, CancellationToken cancellationToken)
    {
        var task = _writeDbContext.WorkTasks.Local.FirstOrDefault(x => x.Id == taskId)
                   ?? await _writeDbContext.WorkTasks.AsTracking().FirstAsync(x => x.Id == taskId, cancellationToken);
        await UpsertTaskAssigneeAsync(task, employeeId, true, actorId, cancellationToken);
    }

    public IQueryable<CustomerFollowUpTaskAssigneeDto> ProjectAssignees(Guid taskId)
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

    public async Task PublishAssigneeNotificationAsync(WorkTask task, Guid employeeId, ViewerScope scope, CancellationToken cancellationToken)
    {
        if (employeeId == scope.EmployeeId)
        {
            return;
        }

        var payload = JsonSerializer.Serialize(new { contentType = "CustomerFollowUpTask", workTaskId = task.Id });
        await _notificationService.PublishAsync(new PublishNotificationRequest
        {
            CompanyId = scope.CompanyId,
            CreatedBy = scope.EmployeeId,
            Topic = TopicNotifications.CustomerFollowUpTaskAssigneeAdded,
            Severity = task.Priority == WorkTaskPriority.Urgent ? NotificationSeverity.Warning : NotificationSeverity.Info,
            Title = task.Title,
            Message = task.NextAction ?? task.Description ?? task.Title,
            Link = $"/crm/follow-up-tasks/{task.Id}",
            PayloadJson = payload,
            TargetUserIds = new[] { employeeId }
        }, cancellationToken);
    }

    public async Task<Customer?> GetTaskCustomerForUpdateAsync(Guid taskId, Guid companyId, CancellationToken cancellationToken)
    {
        var customerId = await _readDbContext.WorkTaskReferences.AsNoTracking()
            .Where(x => x.WorkTaskId == taskId && x.ReferenceType == WorkReferenceType.Customer)
            .OrderByDescending(x => x.IsPrimary)
            .Select(x => (Guid?)x.ReferenceId)
            .FirstOrDefaultAsync(cancellationToken);

        if (!customerId.HasValue)
        {
            customerId = await _readDbContext.WorkTaskReferences.AsNoTracking()
                .Where(x => x.WorkTaskId == taskId && x.ReferenceType == WorkReferenceType.CustomerInteraction)
                .Join(
                    _readDbContext.CustomerInteractions.AsNoTracking().Where(interaction => interaction.CompanyId == companyId),
                    reference => reference.ReferenceId,
                    interaction => interaction.Id,
                    (_, interaction) => (Guid?)interaction.CustomerId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return customerId.HasValue
            ? await _writeDbContext.Customers.AsTracking().FirstOrDefaultAsync(x => x.CustomerId == customerId && x.CompanyId == companyId, cancellationToken)
            : null;
    }

    public async Task<DateTime?> ResolveNextFollowUpAsync(
        Guid customerId,
        Guid companyId,
        Guid? excludedTaskId,
        DateTime? candidate,
        CancellationToken cancellationToken)
    {
        var interactionIds = _readDbContext.CustomerInteractions
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.CustomerId == customerId)
            .Select(x => x.Id);

        var query = _readDbContext.WorkTasks.AsNoTracking().Where(x =>
            x.CompanyId == companyId &&
            x.IsActive &&
            x.DueDate.HasValue &&
            x.Status != WorkTaskStatus.Done &&
            x.Status != WorkTaskStatus.Canceled &&
            x.References.Any(r =>
                (r.ReferenceType == WorkReferenceType.Customer && r.ReferenceId == customerId) ||
                (r.ReferenceType == WorkReferenceType.CustomerInteraction && interactionIds.Contains(r.ReferenceId))));

        if (excludedTaskId.HasValue)
        {
            query = query.Where(x => x.Id != excludedTaskId.Value);
        }

        var existing = await query.MinAsync(x => x.DueDate, cancellationToken);
        if (!candidate.HasValue)
        {
            return existing;
        }

        return !existing.HasValue || candidate < existing ? candidate : existing;
    }

    public static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
