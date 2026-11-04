using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Features.NotificationHub.Queries.GetItems;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Entities.Notifications;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.NotificationHub;

public sealed class NotificationHubUrgentQueryTests
{
    private readonly Guid _employee = Guid.NewGuid();

    [Theory]
    [InlineData(false, false, true, true)]
    [InlineData(true, false, true, false)]
    [InlineData(false, true, true, false)]
    [InlineData(false, false, false, false)]
    public void RequiresOtherSendersLiveUrgentMessageAndActiveMembership(
        bool ownMessage, bool deleted, bool member, bool expected)
    {
        var conversation = Conversation();
        conversation.Participants.Single().IsActive = member;
        var message = conversation.Messages.Single();
        message.SenderEmployeeId = ownMessage ? _employee : Guid.NewGuid();
        message.IsDeleted = deleted;
        // A read urgent message remains urgent. Read receipts must not remove it.
        message.ReadStates.Add(new() { EmployeeId = _employee, IsRead = true });
        var notification = new Notification { Id = Guid.NewGuid() };
        var inbox = new[] { notification }.AsQueryable();
        var links = new[] { Link(notification, conversation) }.AsQueryable();
        Assert.Equal(expected, NotificationHubUrgentQuery.Apply(inbox, links,
            new[] { conversation }.AsQueryable(), _employee).Any());
        message.IsUrgent = false;
        Assert.Empty(NotificationHubUrgentQuery.Apply(inbox, links,
            new[] { conversation }.AsQueryable(), _employee));
    }

    [Fact]
    public void PreservesAuthorizedScopeAndExcludesStandaloneAndBrokenLinks()
    {
        var conversation = Conversation();
        var other = Conversation();
        var notifications = Enumerable.Range(0, 4).Select(_ => new Notification { Id = Guid.NewGuid() }).ToArray();
        var links = new[] { Link(notifications[0], conversation), Link(notifications[1], other),
            new InternalMailNotificationLink { NotificationId = notifications[2].Id },
            new InternalMailNotificationLink { NotificationId = notifications[3].Id, HasConversation = true }
        }.AsQueryable();
        // The access service already excludes wrong-company, inactive and forbidden areas.
        var scope = new[] { conversation }.AsQueryable();
        Assert.Equal(notifications[0].Id, Assert.Single(NotificationHubUrgentQuery.Apply(
            notifications.AsQueryable(), links, scope, _employee)).Id);
        Assert.Empty(NotificationHubUrgentQuery.Apply(notifications.Skip(1).AsQueryable(), links, scope, _employee));
        Assert.Empty(NotificationHubUrgentQuery.Apply(notifications.AsQueryable(), links,
            Array.Empty<InternalConversation>().AsQueryable(), _employee));
    }

    [Fact]
    public void FiltersBeforePagingEvenWhenUrgentItemsAreBehindMoreThanOneBatch()
    {
        var conversation = Conversation();
        var notifications = Enumerable.Range(1, 205).Select(i => new Notification
        {
            Id = Guid.NewGuid(), CreatedDate = new DateTime(2026, 10, 1).AddMinutes(i)
        }).ToArray();
        var links = notifications.Select((n, i) => i < 3 ? Link(n, conversation) :
            new InternalMailNotificationLink { NotificationId = n.Id }).AsQueryable();
        var filtered = NotificationHubUrgentQuery.Apply(notifications.AsQueryable(), links,
            new[] { conversation }.AsQueryable(), _employee);
        var first = filtered.OrderByDescending(n => n.CreatedDate).ThenByDescending(n => n.Id).Take(2).ToArray();
        Assert.Equal(new[] { notifications[2].Id, notifications[1].Id }, first.Select(n => n.Id));
        var cursor = first[^1];
        var second = filtered.Where(n => n.CreatedDate < cursor.CreatedDate ||
            n.CreatedDate == cursor.CreatedDate && n.Id.CompareTo(cursor.Id) < 0)
            .OrderByDescending(n => n.CreatedDate).ThenByDescending(n => n.Id).Take(2).ToArray();
        Assert.Equal(notifications[0].Id, Assert.Single(second).Id);
    }

    [Fact]
    public void UrgentInboxBeforeLatestSelectionPreservesWinnersAndOrder()
    {
        var urgent = Conversation();
        var ordinary = Conversation();
        ordinary.Messages.Single().IsUrgent = false;
        var events = Enumerable.Range(0, 6).Select(i => new Notification
        {
            Id = Guid.NewGuid(), CreatedDate = new DateTime(2026, 10, 1).AddMinutes(i / 2)
        }).ToArray();
        var inbox = events.AsQueryable();
        var links = events.Select((n, i) => Link(n, i % 2 == 0 ? urgent : ordinary)).AsQueryable();
        var conversations = new[] { urgent, ordinary }.AsQueryable();
        foreach (var group in new string?[] { null, "message", "pricing" })
        {
            var before = NotificationHubUrgentQuery.Apply(
                NotificationHubLatestEventQuery.Apply(inbox, inbox, links, group), links, conversations, _employee);
            var ids = NotificationHubUrgentQuery.Apply(inbox, links, conversations, _employee)
                .Select(n => n.Id).ToArray();
            var restricted = inbox.Where(n => ids.Contains(n.Id));
            var after = NotificationHubLatestEventQuery.Apply(restricted, restricted, links, group);
            Assert.Equal(before.OrderByDescending(n => n.CreatedDate).ThenByDescending(n => n.Id).Select(n => n.Id),
                after.OrderByDescending(n => n.CreatedDate).ThenByDescending(n => n.Id).Select(n => n.Id));
            Assert.Single(after);
        }
    }

    [Fact]
    public void LatestUnreadUrgentCombinationTranslatesToPostgresBeforeLimit()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var company = Guid.NewGuid();
        var inbox = db.Notifications.Where(n => n.CompanyId == company &&
            n.UserStates.Any(s => s.UserId == _employee && !s.IsArchived));
        var links = db.QueryInternalMailNotificationLinks(company);
        var latest = NotificationHubLatestEventQuery.Apply(inbox, inbox, links, "message");
        var unread = NotificationHubUnreadQuery.Apply(latest, inbox, links,
            db.InternalMessages.Where(m => m.ReadStates.Any(s => s.EmployeeId == _employee && !s.IsRead))
                .Select(m => m.InternalConversationId), _employee);
        var sql = NotificationHubUrgentQuery.Apply(unread, links,
            db.InternalConversations.Where(c => c.CompanyId == company && c.IsActive), _employee)
            .OrderByDescending(n => n.CreatedDate).ThenByDescending(n => n.Id).Take(30).ToQueryString();
        Assert.Contains("\"IsUrgent\"", sql);
        Assert.Contains("\"IsDeleted\"", sql);
        Assert.Contains("\"SenderEmployeeId\"", sql);
        Assert.Contains("is_archived", sql);
        Assert.Contains("LIMIT", sql);
    }

    private InternalConversation Conversation()
    {
        var c = new InternalConversation { InternalConversationId = Guid.NewGuid() };
        c.Participants.Add(new() { EmployeeId = _employee, IsActive = true });
        c.Messages.Add(new() { InternalConversationId = c.InternalConversationId, SenderEmployeeId = Guid.NewGuid(), IsUrgent = true });
        return c;
    }

    private static InternalMailNotificationLink Link(Notification n, InternalConversation c) => new()
    { NotificationId = n.Id, ConversationId = c.InternalConversationId, HasConversation = true };
}
