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

    [Theory]
    [InlineData(null)]
    [InlineData("message")]
    [InlineData("quotation")]
    public void SqlSideUnreadAfterWinnersPreservesStagedResultOrderAndPages(string? eventGroup)
    {
        var conversations = Enumerable.Range(0, 7).Select(_ => Guid.NewGuid()).ToArray();
        var all = Enumerable.Range(0, 16).Select(i =>
        {
            var n = Event(i % 3 != 0, i % 50);
            n.CreatedDate = new DateTime(2026, 10, 4).AddMinutes(i / 2);
            n.Topic = i % 2 == 0
                ? HRM.Domain.Enums.Notifications.TopicNotifications.SampleRequestMessageCreated
                : HRM.Domain.Enums.Notifications.TopicNotifications.SampleRequestPriceQuoteRequested;
            if (i % 11 == 0) n.UserStates.Single().IsArchived = true;
            if (i % 17 == 0) n.UserStates.Single().UserId = Guid.NewGuid();
            return n;
        }).ToArray();
        var inbox = all.Where(n => n.UserStates.Any(s => s.UserId == _employee && !s.IsArchived)).AsQueryable();
        var links = all.Select((n, i) => new InternalMailNotificationLink
        {
            NotificationId = n.Id, HasConversation = i % 9 != 0,
            ConversationId = i % 9 == 0 ? null : conversations[i % 7]
        }).AsQueryable();
        var legacy = new[] { conversations[2], conversations[5] }.AsQueryable();
        var ids = NotificationHubUnreadQuery.Apply(inbox, inbox, links, legacy, _employee).Select(n => n.Id).ToArray();
        var before = NotificationHubLatestEventQuery.Apply(inbox.Where(n => ids.Contains(n.Id)), inbox, links, eventGroup);
        var after = NotificationHubUnreadQuery.Apply(
            NotificationHubLatestEventQuery.Apply(inbox, inbox, links, eventGroup), inbox, links, legacy, _employee);
        var ownUnreadIds = inbox.Where(n => n.UserStates.Any(s => s.UserId == _employee && !s.IsRead && !s.IsArchived))
            .Select(n => n.Id);
        var conversationKeys = links.Where(l => l.ConversationId.HasValue && ownUnreadIds.Contains(l.NotificationId))
            .Select(l => l.ConversationId!.Value).Concat(legacy).Distinct().ToArray();
        var scoped = NotificationHubLatestEventQuery.Apply(
            NotificationHubUnreadQuery.ApplyConversationScopes(inbox, inbox, links, conversationKeys, _employee),
            inbox, links, eventGroup);
        var restricted = NotificationHubLatestEventQuery.Apply(
            NotificationHubUnreadQuery.ApplyConversationScopes(inbox, inbox, links, conversationKeys, _employee),
            inbox, links, eventGroup, conversationKeys);
        if (eventGroup is not null)
        {
            var topics = HRM.Domain.Enums.Notifications.NotificationTopicCatalog.GetTopics(null, eventGroup);
            before = before.Where(n => topics.Contains(n.Topic));
            after = after.Where(n => topics.Contains(n.Topic));
            scoped = scoped.Where(n => topics.Contains(n.Topic));
            restricted = restricted.Where(n => topics.Contains(n.Topic));
        }
        for (var page = 0; page < 3; page++)
        {
            Assert.Equal(before.OrderByDescending(n => n.CreatedDate).ThenByDescending(n => n.Id).Skip(page * 5).Take(5).Select(n => n.Id).ToArray(),
                restricted.OrderByDescending(n => n.CreatedDate).ThenByDescending(n => n.Id).Skip(page * 5).Take(5).Select(n => n.Id).ToArray());
            Assert.Equal(before.OrderByDescending(n => n.CreatedDate).ThenByDescending(n => n.Id).Skip(page * 5).Take(5).Select(n => n.Id).ToArray(),
                after.OrderByDescending(n => n.CreatedDate).ThenByDescending(n => n.Id).Skip(page * 5).Take(5).Select(n => n.Id).ToArray());
            Assert.Equal(before.OrderByDescending(n => n.CreatedDate).ThenByDescending(n => n.Id).Skip(page * 5).Take(5).Select(n => n.Id).ToArray(),
                scoped.OrderByDescending(n => n.CreatedDate).ThenByDescending(n => n.Id).Skip(page * 5).Take(5).Select(n => n.Id).ToArray());
        }
    }

    [Fact]
    public void ConversationKeysDoNotDropStandaloneUnreadAndDoNotIncludeOtherOrArchivedStates()
    {
        var unread = Event(false, 1);
        var read = Event(true, 2);
        var archived = Event(false, 3);
        archived.UserStates.Single().IsArchived = true;
        var other = Event(false, 4);
        other.UserStates.Single().UserId = Guid.NewGuid();
        var inbox = new[] { unread, read, archived, other }.AsQueryable();
        var links = inbox.Select(n => new InternalMailNotificationLink { NotificationId = n.Id }).AsQueryable();
        Assert.Equal(unread.Id, Assert.Single(NotificationHubUnreadQuery.ApplyConversationScopes(
            inbox, inbox, links, [], _employee)).Id);
    }
}
