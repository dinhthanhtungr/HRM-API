using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Features.InternalMail.Services;
using HRM.Domain.Entities.Notifications;
using HRM.Domain.Enums.InternalMailEnums;
using HRM.Domain.Enums.Notifications;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.InternalMail;

public sealed partial class InternalMailAreaAccessTests
{
    [Theory]
    [InlineData("Developer", false)]
    [InlineData("President", false)]
    [InlineData("LabUser", false)]
    [InlineData("SaleUser", false)]
    [InlineData("SaleUser", true)]
    public async Task CandidateInboxMatchesFullAuthorizationAcrossBatchesAndKeepsTenantInboxScope(string role, bool member)
    {
        var user = new User(role);
        var store = CreateStore(user, InternalMailRelatedType.ConversationPricing, member);
        var memory = (MemoryStore)(object)store.Db;
        var company = user.CompanyId!.Value;
        var employee = user.EmployeeId!.Value;
        var states = new List<NotificationUserState>();
        var notifications = new List<Notification>();
        var links = new List<InternalMailNotificationLink>();
        for (var index = 0; index < 12; index++)
        {
            var notification = new Notification { Id = Guid.NewGuid(), CompanyId = company, Topic = TopicNotifications.InternalMailMessageCreated };
            notifications.Add(notification);
            states.Add(new() { NotificationId = notification.Id, Notification = notification, UserId = employee,
                IsRead = index == 0, IsArchived = index == 1 });
            links.Add(new() { NotificationId = notification.Id, ConversationId = store.Child.InternalConversationId, HasConversation = true });
        }
        var foreign = new Notification { Id = Guid.NewGuid(), CompanyId = Guid.NewGuid() };
        notifications.Add(foreign);
        states.Add(new() { NotificationId = foreign.Id, Notification = foreign, UserId = employee });
        links.Add(new() { NotificationId = foreign.Id, ConversationId = store.Child.InternalConversationId, HasConversation = true });
        var otherEmployee = new Notification { Id = Guid.NewGuid(), CompanyId = company };
        notifications.Add(otherEmployee);
        states.Add(new() { NotificationId = otherEmployee.Id, Notification = otherEmployee, UserId = Guid.NewGuid() });
        links.Add(new() { NotificationId = otherEmployee.Id, ConversationId = store.Child.InternalConversationId, HasConversation = true });
        memory.Values[nameof(store.Db.NotificationUserStates)] = new MemorySet<NotificationUserState>(states);
        memory.Values[nameof(store.Db.Notifications)] = new MemorySet<Notification>(notifications);
        memory.Values[nameof(store.Db.QueryInternalMailNotificationLinks)] = new AsyncQuery<InternalMailNotificationLink>(links);
        var areas = new InternalMailAreaAccessService(store.Db, user);
        var expected = await areas.States().Where(s => !s.IsRead && !s.IsArchived && s.Notification.CompanyId == company)
            .Select(s => s.NotificationId).OrderBy(id => id).ToArrayAsync();
        var actual = await areas.UnreadCandidateStatesAsync(store.Child.InternalConversationId, default);
        Assert.Equal(expected, await actual.Query.Select(s => s.NotificationId).OrderBy(id => id).ToArrayAsync());
        Assert.Equal(expected, actual.EventIds.OrderBy(id => id));
        var batched = await areas.AuthorizeInboxEventIdsAsync(
            Enumerable.Range(0, 4100).Select(_ => Guid.NewGuid()).Concat(expected).ToArray(), false, default);
        Assert.Equal(expected, batched.OrderBy(id => id));
        var wholeInbox = await areas.UnreadCandidateStatesAsync(null, default);
        Assert.Equal(expected, wholeInbox.EventIds.OrderBy(id => id));
        var expectedBadges = await areas.BadgeStates().Where(s => !s.IsRead && !s.IsArchived &&
                s.Notification.CompanyId == company).Select(s => s.NotificationId).OrderBy(id => id).ToArrayAsync();
        var actualBadges = await areas.BadgeStates(wholeInbox.Query).Select(s => s.NotificationId).OrderBy(id => id).ToArrayAsync();
        Assert.Equal(expectedBadges, actualBadges);
        Assert.Equal(expectedBadges, await areas.BadgeStates(await areas.UnreadBadgeStatesAsync(default))
            .Select(s => s.NotificationId).OrderBy(id => id).ToArrayAsync());
        var forged = await areas.AuthorizeInboxEventIdsAsync([foreign.Id, otherEmployee.Id, Guid.NewGuid()], false, default);
        Assert.Empty(forged);
        user.CompanyId = Guid.NewGuid();
        Assert.Empty((await areas.UnreadCandidateStatesAsync(store.Child.InternalConversationId, default)).EventIds);
    }
}
