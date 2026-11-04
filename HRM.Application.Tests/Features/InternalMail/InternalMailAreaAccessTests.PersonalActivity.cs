using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Features.InternalMail.Services;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Entities.Notifications;
using HRM.Domain.Enums.InternalMailEnums;
using HRM.Domain.Enums.Notifications;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HRM.Application.Tests.Features.InternalMail;

public sealed partial class InternalMailAreaAccessTests
{
    [Fact]
    public void PersonalLatestWinnerUsesTheMentionedMessageNotAnUnrelatedNewerEvent()
    {
        var user = new User("SaleUser");
        var store = CreateStore(user, InternalMailRelatedType.Internal, true);
        var message = store.Db.InternalMessages.Single();
        message.SenderEmployeeId = Guid.NewGuid();
        message.PayloadJson = null;
        message.ReplyToMessage = new InternalMessage { SenderEmployeeId = user.EmployeeId!.Value,
            InternalConversationId = message.InternalConversationId };
        var mine = new Notification { Id = Guid.NewGuid(), Topic = TopicNotifications.InternalMailMessageCreated,
            CreatedDate = new DateTime(2026, 11, 1) };
        var other = new Notification { Id = Guid.NewGuid(), Topic = TopicNotifications.InternalMailMessageCreated,
            CreatedDate = mine.CreatedDate.AddMinutes(1) };
        ((MemoryStore)(object)store.Db).Values["QueryInternalMailNotificationLinks"] = new[] {
            new InternalMailNotificationLink { NotificationId = mine.Id, ConversationId = message.InternalConversationId, HasConversation = true, MessageId = message.InternalMessageId },
            new InternalMailNotificationLink { NotificationId = other.Id, ConversationId = message.InternalConversationId, HasConversation = true, MessageId = Guid.NewGuid() }
        }.AsQueryable();
        var access = new InternalMailAreaAccessService(store.Db, user);
        var inbox = new[] { mine, other }.AsQueryable();
        var personal = access.MentionedHubEvents(inbox);
        Assert.Equal(mine.Id, Assert.Single(personal).Id);
        Assert.Equal(mine.Id, Assert.Single(access.LatestHubEvents(personal, personal, null)).Id);
        Assert.Equal(other.Id, Assert.Single(access.LatestHubEvents(inbox, inbox, null)).Id);
    }

    [Fact]
    public void PersonalQueriesTranslateWithExactMessagePrivacyAndSeparateUnreadScope()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var user = new User("SaleUser");
        var access = new InternalMailAreaAccessService(db, user, reactionOptions: Options.Create(new InternalMailReactionOptions { Enabled = true }));
        var personal = access.MentionedHubEvents(access.Notifications(db.Notifications));
        var sql = personal.ToQueryString();
        Assert.Contains("MessageId", sql);
        Assert.Contains("InternalMessageReactions", sql);
        Assert.Contains("CompanyId", sql);
        Assert.Contains("AspNetUserRoles", sql);
        Assert.NotEmpty(access.PersonalReactionUnreadCounts().ToQueryString());
        Assert.NotEmpty(access.ReactionNoticeDeliveryRecipients(new Notification {
            Id = Guid.NewGuid(), CompanyId = user.CompanyId!.Value,
            Topic = TopicNotifications.InternalMailMessageReacted
        }).ToQueryString());
        var grouped = db.InternalMessages.GroupBy(m => m.InternalConversationId).Select(g => new {
            Id = g.Key,
            Latest = g.OrderByDescending(m => m.SentAt).ThenByDescending(m => m.InternalMessageId)
                .Select(m => new { Id = m.InternalMessageId, IsReply = m.ReplyToMessage != null && m.ReplyToMessage.SenderEmployeeId == user.EmployeeId }).First(),
            Count = g.Count(m => m.ReadStates.Any(s => s.EmployeeId == user.EmployeeId && !s.IsRead))
        }).ToQueryString();
        Assert.Contains("GROUP BY", grouped);
        var allUnread = db.QueryInternalConversationNotificationUnreadCounts(user.CompanyId!.Value, user.EmployeeId!.Value).ToQueryString();
        Assert.Contains("<> ALL", allUnread);
    }
}
