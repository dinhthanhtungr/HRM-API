using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Features.InternalMail.Services;
using HRM.Domain.Entities.Notifications;
using HRM.Domain.Enums.InternalMailEnums;

namespace HRM.Application.Tests.Features.InternalMail;

public sealed partial class InternalMailAreaAccessTests
{
    [Theory]
    [InlineData("SaleUser", InternalMailRelatedType.ConversationTechnical, true)]
    [InlineData("SaleUser", InternalMailRelatedType.ConversationTechnical, false)]
    [InlineData("LabUser", InternalMailRelatedType.ConversationPricing, true)]
    [InlineData("President", InternalMailRelatedType.ConversationTechnical, false)]
    [InlineData("Developer", InternalMailRelatedType.ConversationPricing, false)]
    public void CandidateConversationScopePreservesMembershipObserverAndCompanyGates(
        string role, InternalMailRelatedType type, bool member)
    {
        var user = new User(role);
        var store = CreateStore(user, type, member);
        var access = new InternalMailAreaAccessService(store.Db, user);
        var ids = new[] { store.Child.InternalConversationId, Guid.NewGuid() };
        void AssertParity() => Assert.Equal(
            access.Conversations().Where(c => ids.Contains(c.InternalConversationId)).Select(c => c.InternalConversationId).OrderBy(id => id),
            access.ConversationsForCandidates(ids).Select(c => c.InternalConversationId).OrderBy(id => id));
        AssertParity();
        Assert.Empty(access.ConversationsForCandidates([]));
        foreach (var participant in store.Child.Participants) participant.IsActive = false;
        AssertParity();
        user.CompanyId = Guid.NewGuid();
        Assert.Empty(access.ConversationsForCandidates(ids));
    }

    [Fact]
    public async Task LargeUnreadHistoryFallsBackWithoutTruncatingWinnerSelection()
    {
        var user = new User("SaleUser");
        var store = CreateStore(user, InternalMailRelatedType.Internal, true);
        var thread = store.Child.InternalConversationId;
        var notices = Enumerable.Range(1, 4097).Select(i => new Notification {
            Id = Guid.Parse($"00000000-0000-0000-0000-{i:x12}"),
            CreatedDate = new DateTime(2026, 11, 1),
            Topic = HRM.Domain.Enums.Notifications.TopicNotifications.QuotationRequested
        }).Reverse().ToArray();
        ((MemoryStore)(object)store.Db).Values[nameof(store.Db.QueryInternalMailNotificationLinks)] =
            new AsyncQuery<InternalMailNotificationLink>(notices.Select(n => new InternalMailNotificationLink {
                NotificationId = n.Id, ConversationId = thread, HasConversation = true
            }));
        var inbox = new AsyncQuery<Notification>(notices);
        var access = new InternalMailAreaAccessService(store.Db, user);
        var actual = await access.LatestUnreadHubEventsAsync(inbox, inbox, null, [thread], default, []);
        Assert.Equal(notices[0].Id, Assert.Single(actual.Take(1)).Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("message")]
    [InlineData("pricing")]
    public async Task StagedUnreadScopesKeepReadLatestStandaloneAndBrokenLinkSemantics(string? eventGroup)
    {
        var user = new User("SaleUser");
        var store = CreateStore(user, InternalMailRelatedType.Internal, true);
        var notices = Enumerable.Range(0, 5).Select(i => new Notification {
            Id = Guid.Parse($"00000000-0000-0000-0000-{i + 1:x12}"), CompanyId = user.CompanyId!.Value,
            CreatedDate = new DateTime(2026, 11, 1),
            Topic = i == 1 ? HRM.Domain.Enums.Notifications.TopicNotifications.QuotationMessageCreated
                : HRM.Domain.Enums.Notifications.TopicNotifications.QuotationRequested
        }).ToArray();
        for (var i = 0; i < notices.Length; i++)
            notices[i].UserStates.Add(new() { NotificationId = notices[i].Id,
                UserId = user.EmployeeId!.Value, IsRead = i == 1 || i == 3 });
        var proxy = (MemoryStore)(object)store.Db;
        proxy.Values[nameof(store.Db.QueryInternalMailNotificationLinks)] =
            new AsyncQuery<InternalMailNotificationLink>([
                new() { NotificationId = notices[0].Id, ConversationId = store.Child.InternalConversationId, HasConversation = true },
                new() { NotificationId = notices[1].Id, ConversationId = store.Child.InternalConversationId, HasConversation = true },
                new() { NotificationId = notices[2].Id, HasConversation = false },
                new() { NotificationId = notices[3].Id, HasConversation = false },
                new() { NotificationId = notices[4].Id, HasConversation = true }
            ]);
        var inbox = new AsyncQuery<Notification>(notices);
        var access = new InternalMailAreaAccessService(store.Db, user);
        var unreadKeys = await access.UnreadHubConversationIdsAsync(inbox, default);
        var keys = unreadKeys.ConversationIds;
        Assert.Equal(access.UnreadHubConversationIds(inbox).OrderBy(id => id), keys.OrderBy(id => id));
        var expected = access.UnreadHubEventsForConversations(inbox, inbox, keys);
        var scope = await access.UnreadHubEventsForConversationsAsync(
            inbox, inbox, keys, default, unreadKeys.SuppressedNotificationIds);
        var actual = scope.Visible;
        Assert.Equal(expected.Select(n => n.Id).OrderBy(id => id), actual.Select(n => n.Id).OrderBy(id => id));
        Assert.Equal(notices.Take(3).Select(n => n.Id).OrderBy(id => id), actual.Select(n => n.Id).OrderBy(id => id));
        var expectedWinners = access.LatestHubEvents(expected, inbox, eventGroup, keys);
        var actualWinners = await access.LatestUnreadHubEventsAsync(actual, inbox, eventGroup, keys, default, scope.StandaloneIds);
        Assert.Equal(expectedWinners.Select(n => n.Id).OrderBy(id => id), actualWinners.Select(n => n.Id).OrderBy(id => id));
        // Removing the newest visible event must not resurrect the older event in all/message.
        var restricted = actual.Where(n => n.Id != notices[1].Id);
        var restrictedWinners = await access.LatestUnreadHubEventsAsync(restricted, inbox, eventGroup, keys, default);
        Assert.Equal(access.LatestHubEvents(restricted, inbox, eventGroup, keys).Select(n => n.Id).OrderBy(id => id),
            restrictedWinners.Select(n => n.Id).OrderBy(id => id));
    }

    [Fact]
    public async Task PageUnreadCountsKeepNotificationAndLegacyTotalsAndRecheckAccess()
    {
        var user = new User("SaleUser");
        var store = CreateStore(user, InternalMailRelatedType.Internal, true);
        var message = store.Db.InternalMessages.Single();
        message.SenderEmployeeId = Guid.NewGuid();
        message.ReadStates.Add(new() { EmployeeId = user.EmployeeId!.Value, IsRead = false });
        var proxy = (MemoryStore)(object)store.Db;
        proxy.Values[nameof(store.Db.QueryInternalConversationNotificationUnreadCounts)] =
            new AsyncQuery<InternalConversationNotificationUnreadCount>([
                new() { ConversationId = store.Child.InternalConversationId, UnreadCount = 4 }
            ]);
        var access = new InternalMailAreaAccessService(store.Db, user);
        var ids = new[] { store.Child.InternalConversationId };
        var expected = access.NotificationUnreadCounts(ids).Single().UnreadCount;
        Assert.Equal(5, expected);
        var actual = await access.PageNotificationUnreadCountsAsync(ids, default);
        Assert.Equal(expected, actual[store.Child.InternalConversationId]);
        Assert.Empty(await access.PageNotificationUnreadCountsAsync([], default));
        store.Child.Participants.Single().IsActive = false;
        Assert.Empty(await access.PageNotificationUnreadCountsAsync(ids, default));
        store.Child.Participants.Single().IsActive = true;
        user.CompanyId = Guid.NewGuid();
        Assert.Empty(await access.PageNotificationUnreadCountsAsync(ids, default));
    }
}
