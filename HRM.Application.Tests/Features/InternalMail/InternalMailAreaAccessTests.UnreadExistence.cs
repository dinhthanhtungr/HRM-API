using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Features.InternalMail.Services;
using HRM.Domain.Entities.Notifications;
using HRM.Domain.Enums.InternalMailEnums;
using HRM.Domain.Enums.Notifications;

namespace HRM.Application.Tests.Features.InternalMail;

public sealed partial class InternalMailAreaAccessTests
{
    [Theory]
    [InlineData("LabUser")]
    [InlineData("SaleUser")]
    [InlineData("Developer")]
    public void UnreadExistenceRequiresOwnNonArchivedStateAndPreservesVisibility(string role)
    {
        var user = new User(role);
        var store = CreateStore(user, InternalMailRelatedType.Internal, true);
        var memory = (MemoryStore)(object)store.Db;
        var notification = new Notification { Id = Guid.NewGuid(), CompanyId = user.CompanyId!.Value,
            Topic = TopicNotifications.SampleRequestPriceQuoteRequested };
        var state = new NotificationUserState { NotificationId = notification.Id, Notification = notification,
            UserId = user.EmployeeId!.Value };
        memory.Values[nameof(store.Db.NotificationUserStates)] = new MemorySet<NotificationUserState>([state]);
        memory.Values[nameof(store.Db.QueryInternalMailNotificationLinks)] = new AsyncQuery<InternalMailNotificationLink>([
            new() { NotificationId = notification.Id, ConversationId = store.Child.InternalConversationId, HasConversation = true }
        ]);
        var access = new InternalMailAreaAccessService(store.Db, user);
        void Check(bool expected)
        {
            Assert.Equal(expected, access.UnreadConversationIds().Contains(store.Child.InternalConversationId));
            var keys = access.UnreadConversationIds().Distinct().ToArray();
            Assert.Equal(expected, access.InboxConversations(false).Any(row =>
                keys.Contains(row.InternalConversationId) && row.InternalConversationId == store.Child.InternalConversationId));
        }
        Check(true);
        state.IsRead = true;
        Check(false);
        state.IsRead = false; state.IsArchived = true;
        Check(false);
        state.IsArchived = false; state.UserId = Guid.NewGuid();
        Check(false);
        state.UserId = user.EmployeeId.Value; notification.CompanyId = Guid.NewGuid();
        Check(false);
        notification.CompanyId = user.CompanyId.Value;
        store.Child.Participants.Single().IsActive = false;
        Check(role == "Developer");
    }

    [Fact]
    public void UnreadExistenceKeepsEligibleLegacyMessagesButNotSilentWatchers()
    {
        var user = new User("LabUser");
        var store = CreateStore(user, InternalMailRelatedType.Internal, true);
        store.Db.InternalMessages.Single().ReadStates.Add(new() { EmployeeId = user.EmployeeId!.Value, IsRead = false });
        store.Child.Messages.Add(store.Db.InternalMessages.Single());
        var access = new InternalMailAreaAccessService(store.Db, user);
        Assert.Contains(store.Child.InternalConversationId, access.UnreadConversationIds());
        var initialKeys = access.UnreadConversationIds().Distinct().ToArray();
        Assert.Single(access.InboxConversations(false).Where(row => initialKeys.Contains(row.InternalConversationId)));
        store.Child.Participants.Single().IsMuted = true;
        Assert.Empty(access.UnreadConversationIds());
        store.Child.Participants.Single().IsMuted = false;
        store.Child.Participants.Single().IsArchived = true;
        Assert.Empty(access.UnreadConversationIds());
    }
}
