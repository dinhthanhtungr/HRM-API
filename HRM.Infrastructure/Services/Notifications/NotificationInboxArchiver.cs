using HRM.Application.Abstractions.Notifications;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.Notifications;
using HRM.Application.Features.Notifications.Dtos;
using Microsoft.EntityFrameworkCore;

namespace HRM.Infrastructure.Services.Notifications;

internal sealed class NotificationInboxArchiver(INotificationDbContext dbContext, IDateTimeProvider clock) : INotificationInboxArchiver
{
    public Task ArchiveConversationAsync(Guid companyId, Guid employeeId, Guid conversationId, CancellationToken cancellationToken) =>
        ArchiveAsync(companyId, employeeId, notification => NotificationArchiveMatch.Conversation(notification, conversationId), cancellationToken);

    public Task ArchiveDeletedMessageAsync(Guid companyId, Guid conversationId, Guid messageId, CancellationToken cancellationToken) =>
        ArchiveAsync(companyId, null, notification => NotificationArchiveMatch.Conversation(notification, conversationId, messageId), cancellationToken);

    public async Task ArchiveGroupAsync(Guid companyId, Guid employeeId, Guid anchorId, CancellationToken cancellationToken)
    {
        var anchor = await dbContext.Notifications.AsNoTracking()
            .Where(notification => notification.Id == anchorId && notification.CompanyId == companyId &&
                notification.UserStates.Any(state => state.UserId == employeeId && !state.IsArchived))
            .Select(notification => new NotificationDto { Id = notification.Id, Topic = notification.Topic, PayloadJson = notification.PayloadJson })
            .SingleOrDefaultAsync(cancellationToken);
        if (anchor is null) return;
        await ArchiveAsync(companyId, employeeId, notification => NotificationArchiveMatch.Group(notification, anchor), cancellationToken);
    }

    private async Task ArchiveAsync(Guid companyId, Guid? employeeId, Func<NotificationDto, bool> matches, CancellationToken cancellationToken)
    {
        const int pageSize = 200;
        var cutoff = clock.Now;
        DateTime? cursorDate = null;
        var cursorId = Guid.Empty;
        while (true)
        {
            var query = dbContext.Notifications.AsNoTracking().Where(notification =>
                notification.CompanyId == companyId && notification.CreatedDate <= cutoff &&
                notification.UserStates.Any(state => !state.IsArchived && (!employeeId.HasValue || state.UserId == employeeId.Value)));
            if (cursorDate.HasValue)
            {
                var date = cursorDate.Value;
                query = query.Where(notification => notification.CreatedDate > date ||
                    (notification.CreatedDate == date && notification.Id.CompareTo(cursorId) > 0));
            }
            var page = await query.OrderBy(notification => notification.CreatedDate).ThenBy(notification => notification.Id)
                .Select(notification => new NotificationDto
                {
                    Id = notification.Id, Topic = notification.Topic,
                    PayloadJson = notification.PayloadJson, CreatedDate = notification.CreatedDate
                }).Take(pageSize).ToListAsync(cancellationToken);
            if (page.Count == 0) break;
            var ids = page.Where(matches).Select(notification => notification.Id).ToArray();
            if (ids.Length > 0)
            {
                await dbContext.NotificationUserStates.Where(state => ids.Contains(state.NotificationId) &&
                    state.Notification.CompanyId == companyId && !state.IsArchived &&
                    (!employeeId.HasValue || state.UserId == employeeId.Value))
                    .ExecuteUpdateAsync(setters => setters.SetProperty(state => state.IsArchived, true), cancellationToken);
            }
            cursorDate = page[^1].CreatedDate;
            cursorId = page[^1].Id;
            if (page.Count < pageSize) break;
        }
    }
}
