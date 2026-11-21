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
    public async Task PersonalReactionCountUsesRequestScopeButReadsCurrentReadAndArchiveState()
    {
        var user = new User("SaleUser");
        var store = CreateStore(user, InternalMailRelatedType.Internal, true);
        var message = store.Db.InternalMessages.Single();
        message.SenderEmployeeId = user.EmployeeId!.Value;
        var notice = new Notification { Id = Guid.NewGuid(), CompanyId = user.CompanyId!.Value,
            Topic = TopicNotifications.InternalMailMessageReacted };
        var state = new NotificationUserState { Notification = notice, NotificationId = notice.Id, UserId = user.EmployeeId.Value };
        var actor = new HRM.Domain.Entities.HrSchema.Employee { EmployeeId = Guid.NewGuid(), CompanyId = user.CompanyId.Value, IsActive = true };
        var memory = (MemoryStore)(object)store.Db;
        memory.Values[nameof(store.Db.QueryInternalMailNotificationLinks)] = new AsyncQuery<InternalMailNotificationLink>([
            new() { NotificationId = notice.Id, ConversationId = message.InternalConversationId, HasConversation = true, MessageId = message.InternalMessageId }
        ]);
        memory.Values[nameof(store.Db.NotificationUserStates)] = new MemorySet<NotificationUserState>([state]);
        memory.Values[nameof(store.Db.InternalMessageReactions)] = new MemorySet<InternalMessageReaction>([
            new() { Message = message, MessageId = message.InternalMessageId, Employee = actor, EmployeeId = actor.EmployeeId }
        ]);
        var access = new InternalMailAreaAccessService(store.Db, user,
            reactionOptions: Options.Create(new InternalMailReactionOptions { Enabled = true }));
        var source = new AsyncQuery<Notification>([notice]);
        var scope = await access.DefaultLabInboxSelectionAsync(source, default,
            candidateConversationIds: [message.InternalConversationId], candidateEventIds: [notice.Id]);
        Assert.Equal(notice.Id, Assert.Single(scope.AuthorizedEventIds!));
        Assert.Equal(1, Assert.Single(access.PersonalReactionUnreadCounts(scope)).UnreadCount);
        Assert.Equal(1, Assert.Single(access.PersonalReactionUnreadCounts()).UnreadCount);
        state.IsRead = true;
        Assert.Empty(access.PersonalReactionUnreadCounts(scope));
        state.IsRead = false;
        state.IsArchived = true;
        Assert.Empty(access.PersonalReactionUnreadCounts(scope));
        state.IsArchived = false;
        actor.IsActive = false;
        Assert.Empty(access.PersonalReactionUnreadCounts(scope with { UserId = Guid.NewGuid() }));
        var next = await access.DefaultLabInboxSelectionAsync(source, default,
            candidateConversationIds: [message.InternalConversationId], candidateEventIds: [notice.Id]);
        Assert.Empty(next.AuthorizedEventIds!);
        Assert.Empty(access.PersonalReactionUnreadCounts(next));
    }

    [Fact]
    public async Task PersonalMentionShortlistOverflowKeepsTheCompleteAuthorizedQuery()
    {
        var user = new User("SaleUser");
        var store = CreateStore(user, InternalMailRelatedType.Internal, true);
        var rows = new[] { Guid.NewGuid(), store.Child.InternalConversationId }.Select(id => new InternalMessage {
            InternalMessageId = Guid.NewGuid(), InternalConversationId = id, Conversation = store.Child,
            SenderEmployeeId = Guid.NewGuid(), ReplyToMessage = new InternalMessage {
                SenderEmployeeId = user.EmployeeId!.Value, InternalConversationId = id
            }
        }).ToArray();
        ((MemoryStore)(object)store.Db).Values[nameof(store.Db.InternalMessages)] = new MemorySet<InternalMessage>(rows);
        var access = new InternalMailAreaAccessService(store.Db, user);
        Assert.Equal(rows[1].InternalMessageId, Assert.Single(await access.PersonalMentionCandidatesAsync(1, default)).InternalMessageId);
    }

    [Fact]
    public async Task ReactionTargetShortlistRechecksActualMessageScopeOnEveryCall()
    {
        var user = new User("SaleUser");
        var store = CreateStore(user, InternalMailRelatedType.Internal, true);
        var message = store.Db.InternalMessages.Single();
        message.SenderEmployeeId = user.EmployeeId!.Value;
        var access = new InternalMailAreaAccessService(store.Db, user);
        var keys = new[] { message.InternalMessageId, Guid.NewGuid() };
        Assert.Equal(message.InternalMessageId, Assert.Single(await access.AuthorizedOwnReactionMessageIdsAsync(keys, default)));
        Assert.Empty(await access.AuthorizedOwnReactionMessageIdsAsync([], default));
        message.IsDeleted = true;
        Assert.Empty(await access.AuthorizedOwnReactionMessageIdsAsync(keys, default));
        message.IsDeleted = false;
        message.InternalConversationId = Guid.NewGuid();
        Assert.Empty(await access.AuthorizedOwnReactionMessageIdsAsync(keys, default));
        message.InternalConversationId = store.Child.InternalConversationId;
        foreach (var participant in store.Child.Participants) participant.IsActive = false;
        Assert.Empty(await access.AuthorizedOwnReactionMessageIdsAsync(keys, default));
        foreach (var participant in store.Child.Participants) participant.IsActive = true;
        user.CompanyId = Guid.NewGuid();
        Assert.Empty(await access.AuthorizedOwnReactionMessageIdsAsync(keys, default));
    }

    [Fact]
    public async Task PersonalLatestWinnerUsesTheMentionedMessageNotAnUnrelatedNewerEvent()
    {
        var user = new User("SaleUser");
        var store = CreateStore(user, InternalMailRelatedType.Internal, true);
        var message = store.Db.InternalMessages.Single();
        message.SenderEmployeeId = Guid.NewGuid();
        message.PayloadJson = null;
        message.ReplyToMessage = new InternalMessage { SenderEmployeeId = user.EmployeeId!.Value,
            InternalConversationId = message.InternalConversationId };
        var mine = new Notification { Id = Guid.NewGuid(), Topic = TopicNotifications.InternalMailMessageCreated,
            CreatedDate = new DateTime(2026, 11, 1), CompanyId = user.CompanyId!.Value };
        var other = new Notification { Id = Guid.NewGuid(), Topic = TopicNotifications.InternalMailMessageCreated,
            CreatedDate = mine.CreatedDate.AddMinutes(1), CompanyId = user.CompanyId!.Value };
        ((MemoryStore)(object)store.Db).Values["QueryInternalMailNotificationLinks"] = new AsyncQuery<InternalMailNotificationLink>(new[] {
            new InternalMailNotificationLink { NotificationId = mine.Id, ConversationId = message.InternalConversationId, HasConversation = true, MessageId = message.InternalMessageId },
            new InternalMailNotificationLink { NotificationId = other.Id, ConversationId = message.InternalConversationId, HasConversation = true, MessageId = Guid.NewGuid() }
        });
        var access = new InternalMailAreaAccessService(store.Db, user);
        Assert.Equal(message.InternalMessageId, Assert.Single(access.MentionedMessages([message.InternalConversationId])).InternalMessageId);
        Assert.Equal(message.InternalMessageId, Assert.Single(await access.PersonalMentionCandidatesAsync(4096, default)).InternalMessageId);
        Assert.Empty(access.MentionedMessages([]));
        Assert.Empty(access.MentionedMessages([Guid.NewGuid()]));
        var inbox = new AsyncQuery<Notification>(new[] { mine, other });
        var personal = access.MentionedHubEvents(inbox);
        Assert.Equal(mine.Id, Assert.Single(personal).Id);
        Assert.Equal(mine.Id, Assert.Single(access.PersonalInboxCandidates(inbox,
            [message.InternalMessageId], [message.InternalConversationId])).Id);
        Assert.Empty(access.PersonalInboxCandidates(inbox, [], []));
        Assert.Equal(mine.Id, Assert.Single(access.MentionedHubEvents(inbox, [message.InternalMessageId])).Id);
        Assert.Empty(access.MentionedHubEvents(inbox, []));
        Assert.Empty(access.MentionedHubEvents(inbox, [Guid.NewGuid()]));
        Assert.Equal(mine.Id, Assert.Single(access.LatestHubEvents(personal, personal, null)).Id);
        Assert.Equal(other.Id, Assert.Single(access.LatestHubEvents(inbox, inbox, null)).Id);
        var bounded = await access.LatestPersonalHubEventsAsync(personal, [mine.Id, other.Id], null, default);
        Assert.Equal(mine.Id, Assert.Single(bounded).Id);
        var visible = access.Notifications(inbox);
        Assert.Equal(mine.Id, Assert.Single(access.MentionedVisibleHubEvents(visible, [message.InternalMessageId])).Id);
        Assert.Equal(mine.Id, Assert.Single(access.MentionedVisibleHubEvents(visible, [message.InternalMessageId], [mine.Id])).Id);
        Assert.Empty(access.MentionedVisibleHubEvents(visible, [message.InternalMessageId], [other.Id]));
        Assert.Empty(access.MentionedVisibleHubEvents(visible, [Guid.NewGuid()]));
        store.Child.Participants.Single().IsActive = false;
        Assert.Empty(access.MentionedMessages([message.InternalConversationId]));
        Assert.Empty(await access.PersonalMentionCandidatesAsync(4096, default));
        Assert.Empty(access.MentionedVisibleHubEvents(visible, [message.InternalMessageId]));
        store.Child.Participants.Single().IsActive = true;
        user.CompanyId = Guid.NewGuid();
        Assert.Empty(access.MentionedMessages([message.InternalConversationId]));
        Assert.Empty(await access.PersonalMentionCandidatesAsync(4096, default));
        Assert.Empty(access.MentionedVisibleHubEvents(access.Notifications(inbox), [message.InternalMessageId]));
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
        Assert.NotEmpty(access.MentionedHubEvents(access.Notifications(db.Notifications), [Guid.NewGuid()]).ToQueryString());
        var reactionOnly = access.MentionedHubEvents(access.Notifications(db.Notifications), []);
        Assert.NotEmpty(reactionOnly.ToQueryString());
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
