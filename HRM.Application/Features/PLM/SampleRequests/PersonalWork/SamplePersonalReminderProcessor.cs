using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Persistence.Work;
using HRM.Application.Features.Notifications.Dtos;
using HRM.Application.Features.Notifications.Services;
using HRM.Domain.Enums.Notifications;
using HRM.Domain.Enums.WorkTaskEnums;
using HRM.Domain.Entities.WorkTaskSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HRM.Application.Features.PLM.SampleRequests.PersonalWork;

/// <summary>Only opted-in private SampleRequest tasks; CRM reminders keep their existing audience.</summary>
public sealed class SamplePersonalReminderProcessor(IPLMWriteDbContext db, IWorkTaskWriteDbContext work,
    IDateTimeProvider clock, INotificationService notifications, IConfiguration configuration)
{
    public async Task<int> ProcessAsync(CancellationToken ct)
    {
        // Deployment opt-in after the owner applies schema; do not poll a table that may not exist yet.
        if (!configuration.GetValue<bool>("SamplePersonalWork:RemindersEnabled")) return 0;
        var now = clock.Now;
        var query = DueTasks(now);
        var tasks = await query.AsNoTracking().OrderBy(t => t.DueDate).ThenBy(t => t.Id).Take(50)
            .Select(t => new { t.Id, t.CompanyId, t.CreatedBy, t.Title,
                SampleRequestId = t.References.Where(r => r.ReferenceType == WorkReferenceType.SampleRequest).Select(r => r.ReferenceId).First() })
            .ToListAsync(ct);
        var count = 0;
        foreach (var task in tasks)
        {
            await using var transaction = await db.BeginTransactionAsync(ct);
            var claimed = await query.Where(t => t.Id == task.Id)
                .ExecuteUpdateAsync(set => set.SetProperty(t => t.DueReminderSentAt, now), ct);
            if (claimed == 0) continue;
            await notifications.PublishAsync(new PublishNotificationRequest
            {
                CompanyId = task.CompanyId, CreatedBy = task.CreatedBy,
                PersonalReminderOwnerId = task.CreatedBy, TargetUserIds = [task.CreatedBy],
                Topic = TopicNotifications.WorkTaskDue, Severity = NotificationSeverity.Info,
                Title = "Nhắc việc riêng / Personal reminder", Message = task.Title,
                AggregateId = task.Id, AggregateCode = task.Title,
                Link = $"/plm/sample-requests/{task.SampleRequestId}",
            }, ct);
            await transaction.CommitAsync(ct);
            count++;
        }
        return count;
    }

    internal IQueryable<WorkTask> DueTasks(DateTime now) =>
        work.WorkTasks.Where(t => t.IsActive && t.CreatedBy == t.AssignedToEmployeeId &&
            t.DueDate.HasValue && t.DueDate <= now && t.DueReminderSentAt == null &&
            t.Status != WorkTaskStatus.Done && t.Status != WorkTaskStatus.Canceled &&
            !t.References.Any(r => r.ReferenceType != WorkReferenceType.SampleRequest) &&
            t.References.Any(r => r.ReferenceType == WorkReferenceType.SampleRequest &&
                db.SampleRequestUserPreferences.Any(p => p.CompanyId == t.CompanyId && p.EmployeeId == t.CreatedBy &&
                    p.SampleRequestId == r.ReferenceId && p.NotifyOnDue && p.ReminderTaskId == t.Id)) &&
            work.Employees.Any(e => e.CompanyId == t.CompanyId && e.EmployeeId == t.CreatedBy && e.IsActive));
}
