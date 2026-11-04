using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Features.NotificationHub.Queries.GetItems;
using HRM.Domain.Entities.Notifications;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.NotificationHub;

public sealed class NotificationHubUnreadQueryTests
{
    private readonly Guid _employee = Guid.NewGuid();

    [Fact]
    public void LatestReadEventStillAppearsWhenOlderNotificationIsUnread()
    {
        var older = Event(false, 1);
        var latest = Event(true, 2);
        var inbox = new[] { older, latest }.AsQueryable();
        var conversation = Guid.NewGuid();
        var links = inbox.Select(n => new InternalMailNotificationLink { NotificationId = n.Id, ConversationId = conversation, HasConversation = true });
        var winners = NotificationHubLatestEventQuery.Apply(inbox, inbox, links, null);
        var result = NotificationHubUnreadQuery.Apply(winners, inbox, links, Array.Empty<Guid>().AsQueryable(), _employee);
        Assert.Equal(latest.Id, Assert.Single(result).Id);
        var eligibleIds = NotificationHubUnreadQuery.Apply(inbox, inbox, links,
            Array.Empty<Guid>().AsQueryable(), _employee).Select(n => n.Id).ToArray();
        var staged = NotificationHubLatestEventQuery.Apply(
            inbox.Where(n => eligibleIds.Contains(n.Id)), inbox, links, null);
        Assert.Equal(result.Select(n => n.Id).ToArray(), staged.Select(n => n.Id).ToArray());
        older.UserStates.Single().IsRead = true;
        Assert.Empty(result);
    }

    [Fact]
    public void UnreadMessageIncludesThreadButNotUnrelatedReadNotification()
    {
        var thread = Event(true, 1);
        var standalone = Event(true, 2);
        var inbox = new[] { thread, standalone }.AsQueryable();
        var conversation = Guid.NewGuid();
        var links = new[] { new InternalMailNotificationLink { NotificationId = thread.Id, ConversationId = conversation, HasConversation = true } }.AsQueryable();
        Assert.Equal(thread.Id, Assert.Single(NotificationHubUnreadQuery.Apply(inbox, inbox, links, new[] { conversation }.AsQueryable(), _employee)).Id);
        Assert.Empty(NotificationHubUnreadQuery.Apply(inbox.Where(n => n.Id != thread.Id), inbox, links, new[] { conversation }.AsQueryable(), _employee));
    }

    [Fact]
    public void UnreadFilterRunsBeforeTakeAndIgnoresOtherUsersAndArchivedStates()
    {
        var unread = Event(false, 1);
        var archived = Event(false, 2);
        archived.UserStates.Single().IsArchived = true;
        var other = Event(false, 3);
        other.UserStates.Single().UserId = Guid.NewGuid();
        var inbox = Enumerable.Range(4, 30).Select(i => Event(true, i)).Concat([unread, archived, other]).AsQueryable();
        var result = NotificationHubUnreadQuery.Apply(inbox, inbox, Array.Empty<InternalMailNotificationLink>().AsQueryable(), Array.Empty<Guid>().AsQueryable(), _employee)
            .OrderByDescending(n => n.CreatedDate).Take(1);
        Assert.Equal(unread.Id, Assert.Single(result).Id);
    }

    [Fact]
    public void QueryTranslatesToPostgresWithScopeAndPagination()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var company = Guid.NewGuid();
        var inbox = db.Notifications.Where(n => n.CompanyId == company && n.UserStates.Any(s => s.UserId == _employee && !s.IsArchived));
        var links = db.QueryInternalMailNotificationLinks(company);
        var latest = NotificationHubLatestEventQuery.Apply(inbox, inbox, links, null);
        var unreadMessages = db.InternalMessages.Where(m => !m.IsDeleted && m.SenderEmployeeId != _employee &&
            m.ReadStates.Any(s => s.EmployeeId == _employee && !s.IsRead)).Select(m => m.InternalConversationId);
        var sql = NotificationHubUnreadQuery.Apply(latest, inbox, links, unreadMessages, _employee)
            .OrderByDescending(n => n.CreatedDate).ThenByDescending(n => n.Id).Take(30).ToQueryString();
        Assert.Contains("is_read", sql);
        Assert.Contains("company_id", sql);
        Assert.Contains("LIMIT", sql);
    }

    private Notification Event(bool read, int minute)
    {
        var notification = new Notification { Id = Guid.NewGuid(), CreatedDate = new DateTime(2026, 10, 4, 10, minute, 0) };
        notification.UserStates.Add(new() { UserId = _employee, IsRead = read });
        return notification;
    }
}
