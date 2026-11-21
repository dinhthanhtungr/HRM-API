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
    public void RestrictingQuotationThreadsDoesNotFilterTopicsBeforeWinnerOrDropStandalone()
    {
        var oldQuote = Event(TopicNotifications.QuotationRequested, 1);
        var newerDifferentCategory = Event(TopicNotifications.InternalMailMessageCreated, 2);
        var standalone = Event(TopicNotifications.QuotationRequested, 3);
        var inbox = new[] { oldQuote, newerDifferentCategory, standalone }.AsQueryable();
        var thread = Guid.NewGuid();
        var links = new[] {
            new InternalMailNotificationLink { NotificationId = oldQuote.Id, ConversationId = thread, HasConversation = true },
            new InternalMailNotificationLink { NotificationId = newerDifferentCategory.Id, ConversationId = thread, HasConversation = true },
            new InternalMailNotificationLink { NotificationId = standalone.Id, HasConversation = false }
        }.AsQueryable();
        var topics = NotificationTopicCatalog.GetTopics(NotificationCategoryCodes.Quotation, null);
        var baseline = NotificationHubLatestEventQuery.Apply(inbox, inbox, links, null).Where(n => topics.Contains(n.Topic));
        var candidate = NotificationHubLatestEventQuery.Apply(inbox, inbox, links, null, [thread]).Where(n => topics.Contains(n.Topic));
        Assert.Equal(baseline.Select(n => n.Id).ToArray(), candidate.Select(n => n.Id).ToArray());
        Assert.Equal(standalone.Id, Assert.Single(candidate).Id);
        Assert.Equal(standalone.Id, Assert.Single(NotificationHubLatestEventQuery.Apply(inbox, inbox, links, null, [])).Id);
    }

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
    public void UuidTieBreakerMatchesCanonicalOrderingAcrossSegments()
    {
        var ids = new[] { "ffffffff-ffff-ffff-ffff-ffffffffffff", "7fffffff-ffff-ffff-ffff-ffffffffffff",
            "80000000-0000-0000-0000-000000000000", "00000000-ffff-ffff-ffff-ffffffffffff" };
        var inbox = ids.Select(id => { var n = Event(TopicNotifications.QuotationRequested, 1); n.Id = Guid.Parse(id); return n; }).AsQueryable();
        var conversationId = Guid.NewGuid();
        var links = inbox.Select(n => new InternalMailNotificationLink
            { NotificationId = n.Id, ConversationId = conversationId, HasConversation = true });
        var expected = inbox.OrderByDescending(n => n.Id).First().Id;
        Assert.Equal(expected, Assert.Single(NotificationHubLatestEventQuery.Apply(inbox, inbox, links, null)).Id);
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
        Assert.Contains("GROUP BY", sql);
        Assert.Contains("max(", sql);
        Assert.Contains("user_id", sql);
        Assert.Contains("is_archived", sql);
        Assert.Contains("LIMIT", sql);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("message")]
    [InlineData("request")]
    [InlineData("pricing")]
    public void GroupedWinnersMatchOriginalRuleAcrossTopicsVisibilityAndPaging(string? group)
    {
        var topics = new[] { TopicNotifications.QuotationRequested,
            TopicNotifications.QuotationPricingApproved, TopicNotifications.QuotationMessageCreated };
        var notifications = Enumerable.Range(0, 24).Select(i =>
        {
            var n = Event(topics[i % topics.Length], i % 10);
            n.Id = Guid.Parse($"00000000-0000-0000-0000-{i + 1:000000000000}");
            return n;
        }).ToArray();
        var conversations = Enumerable.Range(0, 8).Select(_ => Guid.NewGuid()).ToArray();
        var links = notifications.Select((n, i) => new InternalMailNotificationLink
        {
            NotificationId = n.Id,
            HasConversation = i % 13 != 0,
            ConversationId = i % 13 == 0 || i % 17 == 0 ? null : conversations[i % 8]
        }).AsQueryable();
        // Missing/archived recipients are absent from inbox; inaccessible threads
        // and standalone entries can be absent from the visible outer query.
        var inbox = notifications.Where((_, i) => i % 11 != 0).AsQueryable();
        var visible = inbox.Where(n => !links.Any(l => l.NotificationId == n.Id &&
            l.ConversationId == conversations[0]));
        var messageTopics = NotificationTopicCatalog.GetTopics(null, "message");
        var businessOnly = group is not null && group != "message";
        var candidates = businessOnly ? inbox.Where(n => !messageTopics.Contains(n.Topic)) : inbox;
        var linkRows = links.ToArray();
        var candidateRows = candidates.ToArray();
        var expected = visible.AsEnumerable().Where(n => (!businessOnly || !messageTopics.Contains(n.Topic)) &&
            linkRows.Any(l => l.NotificationId == n.Id && (!l.HasConversation ||
                l.ConversationId.HasValue && !candidateRows.Any(other =>
                    linkRows.Any(otherLink => otherLink.NotificationId == other.Id &&
                        otherLink.ConversationId == l.ConversationId) &&
                    (other.CreatedDate > n.CreatedDate || other.CreatedDate == n.CreatedDate && other.Id.CompareTo(n.Id) > 0))))).ToArray();
        var actual = NotificationHubLatestEventQuery.Apply(visible, inbox, links, group).ToArray();
        Guid[] Page(IEnumerable<Notification> source, int skip) => source
            .OrderByDescending(n => n.CreatedDate).ThenByDescending(n => n.Id)
            .Skip(skip).Take(5).Select(n => n.Id).ToArray();
        for (var skip = 0; skip < 30; skip += 5)
            Assert.Equal(Page(expected, skip), Page(actual, skip));
    }

    private static Notification Event(TopicNotifications topic, int minute) => new()
    { Id = Guid.NewGuid(), Topic = topic, CreatedDate = new DateTime(2026, 10, 4, 10, minute, 0) };
}
