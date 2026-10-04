using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Features.NotificationHub.Queries.GetItems;
using HRM.Domain.Entities.Notifications;
using HRM.Domain.Enums.Notifications;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.NotificationHub;

public sealed class NotificationHubLatestEventQueryTests
{
    [Fact]
    public void ChatDoesNotDisplaceBusinessEvent_AndOldRequestDoesNotSurviveFiltering()
    {
        var conversation = Guid.NewGuid();
        var request = Event(TopicNotifications.QuotationRequested, 1);
        var approved = Event(TopicNotifications.QuotationPricingApproved, 2);
        var chat = Event(TopicNotifications.QuotationMessageCreated, 3);
        var inbox = new[] { request, approved, chat }.AsQueryable();
        var links = inbox.Select(n => new InternalMailNotificationLink
            { NotificationId = n.Id, ConversationId = conversation, HasConversation = true });
        var business = NotificationHubLatestEventQuery.Apply(inbox, inbox, links, "pricing").ToArray();
        Assert.Equal(approved.Id, Assert.Single(business).Id);
        Assert.Empty(NotificationHubLatestEventQuery.Apply(inbox, inbox, links, "request")
            .Where(n => n.Topic == TopicNotifications.QuotationRequested));
        Assert.Equal(chat.Id, Assert.Single(NotificationHubLatestEventQuery.Apply(inbox, inbox, links, "message")).Id);
        Assert.Equal(chat.Id, Assert.Single(NotificationHubLatestEventQuery.Apply(inbox, inbox, links, null)).Id);
    }

    [Fact]
    public void InvisibleEventsDoNotSuppressVisibleWinner_StandaloneEventsRemain()
    {
        var request = Event(TopicNotifications.QuotationRequested, 1);
        var standalone = Event(TopicNotifications.WorkTaskDue, 2);
        var inbox = new[] { request, standalone }.AsQueryable();
        var links = new[] {
            new InternalMailNotificationLink { NotificationId = request.Id, ConversationId = Guid.NewGuid(), HasConversation = true },
            new InternalMailNotificationLink { NotificationId = standalone.Id, HasConversation = false }
        }.AsQueryable();
        Assert.Equal(2, NotificationHubLatestEventQuery.Apply(inbox, inbox, links, "request").Count());
        Assert.Single(NotificationHubLatestEventQuery.Apply(inbox.Where(n => n.Id == request.Id), inbox, links, "request"));
    }

    [Fact]
    public void SameTimestampUsesIdTieBreakBeforeCursor()
    {
        var first = Event(TopicNotifications.QuotationRequested, 1);
        var second = Event(TopicNotifications.QuotationSent, 1);
        first.Id = Guid.Parse("00000000-0000-0000-0000-000000000001");
        second.Id = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var inbox = new[] { first, second }.AsQueryable();
        var conversation = Guid.NewGuid();
        var links = inbox.Select(n => new InternalMailNotificationLink { NotificationId = n.Id, ConversationId = conversation, HasConversation = true });
        var winners = NotificationHubLatestEventQuery.Apply(inbox, inbox, links, "status");
        Assert.Equal(second.Id, Assert.Single(winners).Id);
        Assert.Empty(winners.Where(n => n.Id.CompareTo(second.Id) < 0));
    }

    [Fact]
    public void QueryTranslatesToPostgresWithInboxScopeAndPaging()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var company = Guid.NewGuid();
        var employee = Guid.NewGuid();
        var inbox = db.Notifications.Where(n => n.CompanyId == company &&
            n.UserStates.Any(s => s.UserId == employee && !s.IsArchived));
        var query = NotificationHubLatestEventQuery.Apply(inbox, inbox,
            db.QueryInternalMailNotificationLinks(company), "pricing")
            .Where(n => n.Topic == TopicNotifications.QuotationPricingApproved)
            .OrderByDescending(n => n.CreatedDate).ThenByDescending(n => n.Id).Take(20);
        var sql = query.ToQueryString();
        Assert.Contains("NOT EXISTS", sql);
        Assert.Contains("user_id", sql);
        Assert.Contains("is_archived", sql);
        Assert.Contains("LIMIT", sql);
    }

    private static Notification Event(TopicNotifications topic, int minute) => new()
    { Id = Guid.NewGuid(), Topic = topic, CreatedDate = new DateTime(2026, 10, 4, 10, minute, 0) };
}
