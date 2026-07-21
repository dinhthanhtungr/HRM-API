using System.Text.Json;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Features.Notifications.Dtos;
using HRM.Application.Features.Notifications.Services;
using HRM.Domain.Entities.WorkTaskSchema;
using HRM.Domain.Enums.Notifications;
using HRM.Domain.Enums.WorkTaskEnums;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.CustomerCare.Services;

/// <summary>
/// Tạo cảnh báo đến hạn cho follow-up task CRM và resolve người nhận theo assignee/group thực tế.
/// </summary>
internal sealed class CustomerFollowUpTaskDueReminderProcessor
    : ICustomerFollowUpTaskDueReminderProcessor
{
    private const int BatchSize = 50;

    private readonly ICRMWriteDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly INotificationService _notificationService;

    public CustomerFollowUpTaskDueReminderProcessor(
        ICRMWriteDbContext dbContext,
        IDateTimeProvider dateTimeProvider,
        INotificationService notificationService)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
        _notificationService = notificationService;
    }

    public async Task<int> ProcessDueRemindersAsync(
        CancellationToken cancellationToken = default)
    {
        var now = _dateTimeProvider.Now;
        var tasks = await _dbContext.WorkTasks
            .AsTracking()
            .Include(x => x.Assignees.Where(assignee => assignee.IsActive))
            .Where(x =>
                x.IsActive &&
                x.DueDate.HasValue &&
                x.DueDate >= NotificationTopicCategoryRules.CurrentDataStartDate &&
                x.DueDate <= now &&
                x.DueReminderSentAt == null &&
                x.Status != WorkTaskStatus.Done &&
                x.Status != WorkTaskStatus.Canceled &&
                x.References.Any(reference =>
                    reference.ReferenceType == WorkReferenceType.Customer ||
                    reference.ReferenceType == WorkReferenceType.CustomerInteraction))
            .OrderBy(x => x.DueDate)
            .ThenBy(x => x.Id)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        var processedCount = 0;

        foreach (var task in tasks)
        {
            var recipientIds = await ResolveRecipientIdsAsync(task, cancellationToken);

            // PublishAsync dùng cùng scoped DbContext và SaveChanges một lần cho cả reminder flag và notification.
            task.DueReminderSentAt = now;

            if (recipientIds.Count == 0)
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                continue;
            }

            var dueDate = task.DueDate!.Value;
            await _notificationService.PublishAsync(new PublishNotificationRequest
            {
                CompanyId = task.CompanyId,
                CreatedBy = task.CreatedBy,
                Topic = TopicNotifications.CustomerFollowUpTaskDue,
                Severity = NotificationSeverity.Warning,
                Title = "Công việc đến hạn",
                Message = $"{task.Title} đã đến hạn lúc {dueDate:HH:mm dd/MM/yyyy}.",
                Link = $"/crm/follow-up-tasks/{task.Id}",
                PayloadJson = JsonSerializer.Serialize(new
                {
                    contentType = "CustomerFollowUpTask",
                    workTaskId = task.Id,
                    dueDate
                }),
                TargetUserIds = recipientIds
            }, cancellationToken);

            processedCount++;
        }

        return processedCount;
    }

    private async Task<IReadOnlyCollection<Guid>> ResolveRecipientIdsAsync(
        WorkTask task,
        CancellationToken cancellationToken)
    {
        var responsibleEmployeeIds = task.Assignees
            .Select(x => x.EmployeeId)
            .Where(x => x != Guid.Empty)
            .ToHashSet();

        if (task.AssignedToEmployeeId is { } assignedEmployeeId && assignedEmployeeId != Guid.Empty)
        {
            responsibleEmployeeIds.Add(assignedEmployeeId);
        }

        var groupIds = responsibleEmployeeIds.Count == 0
            ? Array.Empty<Guid>()
            : await _dbContext.MemberInGroups
                .AsNoTracking()
                .Where(x =>
                    x.IsActive &&
                    x.Profile.HasValue &&
                    responsibleEmployeeIds.Contains(x.Profile.Value) &&
                    x.Group.CompanyId == task.CompanyId)
                .Select(x => x.GroupId)
                .Distinct()
                .ToArrayAsync(cancellationToken);

        var leaderIds = groupIds.Length == 0
            ? Array.Empty<Guid>()
            : await _dbContext.MemberInGroups
                .AsNoTracking()
                .Where(x =>
                    x.IsActive &&
                    x.IsAdmin == true &&
                    x.Profile.HasValue &&
                    groupIds.Contains(x.GroupId) &&
                    x.Group.CompanyId == task.CompanyId)
                .Select(x => x.Profile!.Value)
                .Distinct()
                .ToArrayAsync(cancellationToken);

        responsibleEmployeeIds.UnionWith(leaderIds);

        if (task.CreatedBy != Guid.Empty)
        {
            responsibleEmployeeIds.Add(task.CreatedBy);
        }

        return await _dbContext.Employees
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == task.CompanyId &&
                x.IsActive &&
                responsibleEmployeeIds.Contains(x.EmployeeId))
            .Select(x => x.EmployeeId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
    }
}
