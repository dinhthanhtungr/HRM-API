using System.Reflection;
using System.Text.Json.Nodes;
using HRM.Application.Features.Notifications.Dtos;
using HRM.Application.Abstractions.Persistence.Notifications;
using HRM.Application.Features.InternalMail.Services;
using HRM.Application.Features.Notifications.Services;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Entities.Notifications;
using HRM.Domain.Enums.InternalMailEnums;
using HRM.Domain.Enums.Notifications;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;

namespace HRM.Application.Tests.Features.InternalMail;

public sealed partial class InternalMailAreaAccessTests
{
    [Theory]
    [InlineData("visible")]
    [InlineData("read")]
    [InlineData("archived")]
    [InlineData("deleted")]
    [InlineData("removed")]
    [InlineData("foreign")]
    [InlineData("disabled")]
    public async Task ReactionSummaryShortlistPreservesVisibilityAndUnreadState(string scenario)
    {
        var fixture = ReactionNotificationFixture();
        await fixture.Sync(fixture.AddActor("Actor"), true);
        fixture.Memory.Values["Notifications"] = new MemorySet<Notification>(fixture.Notifications);
        fixture.Memory.Values["NotificationUserStates"] = new MemorySet<NotificationUserState>(fixture.States);
        fixture.Memory.Values["QueryInternalMailNotificationLinks"] = new AsyncQuery<HRM.Application.Abstractions.Persistence.InternalMail.InternalMailNotificationLink>(
            fixture.Notifications.Select(n => new HRM.Application.Abstractions.Persistence.InternalMail.InternalMailNotificationLink
            { NotificationId = n.Id, HasConversation = true, ConversationId = fixture.Conversation.InternalConversationId,
                MessageId = fixture.Message.InternalMessageId }));
        var state = Assert.Single(fixture.States);
        if (scenario == "read") state.IsRead = true;
        if (scenario == "archived") state.IsArchived = true;
        if (scenario == "deleted") fixture.Message.IsDeleted = true;
        if (scenario == "removed") fixture.Conversation.Participants.Single(p => p.EmployeeId == fixture.Author.EmployeeId).IsActive = false;
        if (scenario == "foreign") fixture.Notifications.Single().CompanyId = Guid.NewGuid();
        foreach (var role in new[] { "SaleUser", "LabUser", "President", "Developer" })
        {
            var user = new User(role) { EmployeeId = fixture.Author.EmployeeId, CompanyId = fixture.Company };
            var areas = new InternalMailAreaAccessService(fixture.Mail, user, reactionOptions:
                Options.Create(new InternalMailReactionOptions { Enabled = scenario != "disabled" }));
            var topics = InternalMailPersonalActivity.ReactionTopics;
            var expected = await areas.States().CountAsync(s => !s.IsRead && !s.IsArchived && topics.Contains(s.Notification.Topic));
            Assert.Equal(expected, await areas.PersonalReactionUnreadCountAsync(default));
            var candidateScope = await areas.UnreadCandidateStatesAsync(fixture.Conversation.InternalConversationId, default);
            Assert.Equal(expected, await candidateScope.Query.CountAsync(s => topics.Contains(s.Notification.Topic)));
            Assert.Empty((await areas.UnreadCandidateStatesAsync(null, default, excludeReactions: true)).EventIds);
            Assert.Empty(await (await areas.UnreadBadgeStatesAsync(default)).ToArrayAsync());
            if (scenario == "visible") Assert.Equal(1, expected);
        }
    }

    [Fact]
    public async Task ReactionNotificationGroupsActorsDoesNotRealertEmojiChangeAndArchivesLastRemoval()
    {
        var fixture = ReactionNotificationFixture();
        var first = fixture.AddActor("First");
        await fixture.Sync(first, true);
        var notification = Assert.Single(fixture.Notifications);
        var id = notification.Id;
        var state = Assert.Single(fixture.States);
        Assert.Equal(fixture.Author.EmployeeId, state.UserId);
        Assert.False(state.IsRead);
        Assert.Equal(fixture.Author.EmployeeId, Assert.Single(fixture.Recipients).TargetUserId);
        Assert.Equal(3, fixture.Outbox.Count);
        Assert.Equal(TopicNotifications.InternalMailMessageReacted, notification.Topic);
        Assert.Equal(1, JsonNode.Parse(notification.PayloadJson!)!["reactionActorCount"]!.GetValue<int>());
        var date = notification.CreatedDate;
        state.IsRead = true;
        state.ReadDate = date;
        fixture.Rows.Single().ReactionType = InternalMessageReactionType.Like;
        await fixture.Sync(first, false);
        Assert.Single(fixture.Notifications);
        Assert.Equal(id, notification.Id);
        Assert.Equal(date, notification.CreatedDate);
        Assert.True(state.IsRead);
        Assert.Equal(4, fixture.Outbox.Count);
        var second = fixture.AddActor("Second");
        await fixture.Sync(second, true);
        Assert.Single(fixture.Notifications);
        Assert.False(state.IsRead);
        Assert.Equal(7, fixture.Outbox.Count);
        Assert.Equal(2, JsonNode.Parse(notification.PayloadJson!)!["reactionActorCount"]!.GetValue<int>());
        fixture.Rows.RemoveAll(r => r.EmployeeId == first);
        state.IsRead = true;
        await fixture.Sync(first, false);
        Assert.True(state.IsRead);
        Assert.False(state.IsArchived);
        Assert.Equal(8, fixture.Outbox.Count);
        Assert.Equal(1, JsonNode.Parse(notification.PayloadJson!)!["reactionActorCount"]!.GetValue<int>());
        fixture.Rows.Clear();
        await fixture.Sync(second, false);
        Assert.True(state.IsArchived);
        Assert.Equal(9, fixture.Outbox.Count);
        Assert.Equal(fixture.MessageSentAt, fixture.Message.SentAt);
        Assert.Equal(fixture.ActivityAt, fixture.Conversation.LastMessageAt);
        Assert.Empty(fixture.Message.ReadStates);
    }

    [Theory]
    [InlineData("self")]
    [InlineData("hidden-developer")]
    [InlineData("author-removed")]
    [InlineData("author-foreign-company")]
    public async Task ReactionNotificationsNeverLeakHiddenOrUnauthorizedActors(string scenario)
    {
        var fixture = ReactionNotificationFixture();
        var actor = scenario == "self" ? fixture.Author.EmployeeId : fixture.AddActor("Actor");
        if (scenario == "hidden-developer")
        {
            var memory = fixture.Memory;
            var account = new HRM.Domain.Identity.ApplicationUser { Id = Guid.NewGuid(), EmployeeId = actor };
            var role = new HRM.Domain.Identity.ApplicationRole { Id = Guid.NewGuid(), Name = "Developer", NormalizedName = "DEVELOPER" };
            memory.Values["Users"] = new MemorySet<HRM.Domain.Identity.ApplicationUser>([account]);
            memory.Values["Roles"] = new MemorySet<HRM.Domain.Identity.ApplicationRole>([role]);
            memory.Values["UserRoles"] = new MemorySet<HRM.Domain.Identity.ApplicationUserRole>([new() { UserId = account.Id, RoleId = role.Id, IsActive = true }]);
        }
        if (scenario == "author-removed") fixture.Conversation.Participants.Single(p => p.EmployeeId == fixture.Author.EmployeeId).IsActive = false;
        if (scenario == "author-foreign-company") fixture.Author.CompanyId = Guid.NewGuid();
        await fixture.Sync(actor, true);
        Assert.Empty(fixture.Notifications);
        Assert.Empty(fixture.States);
        Assert.Empty(fixture.Recipients);
        Assert.Empty(fixture.Outbox);
    }

    [Fact]
    public async Task ReaddingKnownActorRestoresGroupWithoutResettingReadOrActivityOrPushingAgain()
    {
        var fixture = ReactionNotificationFixture();
        var actor = fixture.AddActor("First");
        await fixture.Sync(actor, true);
        var notification = Assert.Single(fixture.Notifications);
        var state = Assert.Single(fixture.States);
        state.IsRead = true;
        state.ReadDate = notification.CreatedDate;
        var date = notification.CreatedDate;
        fixture.Rows.Clear();
        await fixture.Sync(actor, false);
        Assert.True(state.IsArchived);
        fixture.Rows.Add(new() { MessageId = fixture.Message.InternalMessageId, EmployeeId = actor,
            Employee = fixture.Employees.Single(e => e.EmployeeId == actor), ReactionType = InternalMessageReactionType.Heart });
        await fixture.Sync(actor, true);
        Assert.False(state.IsArchived);
        Assert.True(state.IsRead);
        Assert.Equal(date, notification.CreatedDate);
        Assert.Equal(2, fixture.Outbox.Count(o => o.Type != HRM.Application.Features.InternalMail.Reactions.InternalMessageReactionOutbox.PersonalType));
        Assert.Equal(1, JsonNode.Parse(notification.PayloadJson!)!["reactionActorCount"]!.GetValue<int>());
    }

    [Fact]
    public async Task ReactionNotificationReadMasksActorWhoBecomesDeveloperAfterPublication()
    {
        var fixture = ReactionNotificationFixture();
        var actor = fixture.AddActor("Ordinary name now hidden");
        await fixture.Sync(actor, true);
        var notification = Assert.Single(fixture.Notifications);
        var authorUser = new User("SaleUser") { EmployeeId = fixture.Author.EmployeeId, CompanyId = fixture.Company };
        var authorService = new NotificationService(fixture.Db, authorUser, new ObserverPushEnabled(), null!,
            new InternalMailAreaAccessService(fixture.Mail, authorUser), null!, fixture.Mail);
        var account = new HRM.Domain.Identity.ApplicationUser { Id = Guid.NewGuid(), EmployeeId = actor };
        var role = new HRM.Domain.Identity.ApplicationRole { Id = Guid.NewGuid(), Name = "Developer", NormalizedName = "DEVELOPER" };
        fixture.Memory.Values["Users"] = new MemorySet<HRM.Domain.Identity.ApplicationUser>([account]);
        fixture.Memory.Values["Roles"] = new MemorySet<HRM.Domain.Identity.ApplicationRole>([role]);
        fixture.Memory.Values["UserRoles"] = new MemorySet<HRM.Domain.Identity.ApplicationUserRole>([new() { UserId = account.Id, RoleId = role.Id, IsActive = true }]);
        var dto = new NotificationDto { Topic = notification.Topic, PayloadJson = notification.PayloadJson, Message = notification.Message,
            CreatedBy = actor, CreatedByNameSnapshot = "Ordinary name now hidden" };
        NotificationPayloadPresentation.Apply(dto);
        await authorService.EnrichReactionNotificationsAsync([dto], default);
        Assert.Null(dto.CreatedBy);
        Assert.Null(dto.CreatedByNameSnapshot);
        Assert.DoesNotContain("Ordinary name", dto.Message);
        Assert.Equal(0, JsonNode.Parse(dto.PayloadJson!)!["reactionActorCount"]!.GetValue<int>());
        Assert.Null(JsonNode.Parse(dto.PayloadJson!)!["seenReactionActorIds"]);
    }

    [Fact]
    public void ReactionNotificationVisibilityQueriesNewTableOnlyWhenEnabled()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var user = new User("SaleUser");
        var disabled = new InternalMailAreaAccessService(db, user).FilterReactionVisibility(db.Notifications).ToQueryString();
        Assert.DoesNotContain("InternalMessageReactions", disabled);
        var enabled = new InternalMailAreaAccessService(db, user, null,
            Options.Create(new InternalMailReactionOptions { Enabled = true }))
            .FilterReactionVisibility(db.Notifications).ToQueryString();
        Assert.Contains("InternalMessageReactions", enabled);
        Assert.Contains("SenderEmployeeId", enabled);
        Assert.Contains("CompanyId", enabled);
        Assert.Contains("AspNetUserRoles", enabled);
    }

    private static ReactionNotificationTestFixture ReactionNotificationFixture()
    {
        var user = new User("SaleUser");
        var source = CreateStore(user, InternalMailRelatedType.Internal, true);
        var memory = (MemoryStore)(object)source.Db;
        var author = new Employee { EmployeeId = Guid.NewGuid(), CompanyId = user.CompanyId!.Value, FullName = "Author", IsActive = true };
        source.Child.Participants.Add(new() { EmployeeId = author.EmployeeId, IsActive = true });
        var employees = new List<Employee> { author };
        memory.Values["Employees"] = new MemorySet<Employee>(employees);
        var message = source.Db.InternalMessages.Single();
        message.SenderEmployeeId = author.EmployeeId;
        message.Body = "Original message";
        var rows = new List<InternalMessageReaction>();
        memory.Values["InternalMessageReactions"] = new MemorySet<InternalMessageReaction>(rows);
        var notifications = new List<Notification>();
        var recipients = new List<NotificationRecipient>();
        var states = new List<NotificationUserState>();
        var outbox = new List<OutboxMessage>();
        var notificationDb = DispatchProxy.Create<INotificationDbContext, MemoryStore>();
        var proxy = (MemoryStore)(object)notificationDb;
        proxy.Values["Notifications"] = new ReactionNotificationSet<Notification>(notifications);
        proxy.Values["NotificationRecipients"] = new ReactionNotificationSet<NotificationRecipient>(recipients);
        proxy.Values["NotificationUserStates"] = new ReactionNotificationSet<NotificationUserState>(states, state =>
        {
            state.Notification = notifications.Single(n => n.Id == state.NotificationId);
            state.Notification.UserStates.Add(state);
        });
        proxy.Values["OutboxMessages"] = new ReactionNotificationSet<OutboxMessage>(outbox);
        var service = new NotificationService(notificationDb, user, new ObserverPushEnabled(), null!,
            new InternalMailAreaAccessService(source.Db, user), null!, source.Db);
        return new(user.CompanyId.Value, source.Child, message, author, memory, employees, rows, notifications, recipients, states, outbox, service, notificationDb, source.Db);
    }

    private sealed record ReactionNotificationTestFixture(Guid Company, InternalConversation Conversation,
        InternalMessage Message, Employee Author, MemoryStore Memory, List<Employee> Employees,
        List<InternalMessageReaction> Rows, List<Notification> Notifications, List<NotificationRecipient> Recipients,
        List<NotificationUserState> States, List<OutboxMessage> Outbox, NotificationService Service,
        INotificationDbContext Db, HRM.Application.Abstractions.Persistence.InternalMail.IInternalMailDbContext Mail)
    {
        public DateTime MessageSentAt { get; } = Message.SentAt;
        public DateTime? ActivityAt { get; } = Conversation.LastMessageAt;
        public Guid AddActor(string name)
        {
            var actor = new Employee { EmployeeId = Guid.NewGuid(), CompanyId = Company, FullName = name, IsActive = true };
            Employees.Add(actor);
            Rows.Add(new() { MessageId = Message.InternalMessageId, EmployeeId = actor.EmployeeId, Employee = actor,
                ReactionType = InternalMessageReactionType.Heart, CreatedAt = DateTime.Now });
            return actor.EmployeeId;
        }
        public Task Sync(Guid actor, bool added) => Service.SynchronizeAsync(Company, Conversation.InternalConversationId, Message.InternalMessageId, actor, added, default);
    }
    private sealed class ReactionNotificationSet<T>(List<T> rows, Action<T>? onAdd = null) : MemorySet<T>(rows) where T : class
    {
        public override EntityEntry<T> Add(T entity) { rows.Add(entity); onAdd?.Invoke(entity); return null!; }
    }
}
