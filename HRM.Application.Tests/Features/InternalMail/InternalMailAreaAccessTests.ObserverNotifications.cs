using System.Reflection;
using HRM.Application.Abstractions.Notifications;
using HRM.Application.Abstractions.Persistence.Notifications;
using HRM.Application.Features.InternalMail.Services;
using HRM.Application.Features.Notifications.Dtos;
using HRM.Application.Features.Notifications.Services;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Entities.Notifications;
using HRM.Domain.Enums.InternalMailEnums;
using HRM.Domain.Enums.Notifications;

namespace HRM.Application.Tests.Features.InternalMail;

public sealed partial class InternalMailAreaAccessTests
{
    [Theory]
    [InlineData(InternalMailRelatedType.Internal)]
    [InlineData(InternalMailRelatedType.SampleRequest)]
    [InlineData(InternalMailRelatedType.Quotation)]
    [InlineData(InternalMailRelatedType.ConversationTechnical)]
    [InlineData(InternalMailRelatedType.ConversationPricing)]
    [InlineData(InternalMailRelatedType.ConversationCustom)]
    public async Task PublishExactEmptyAudienceStillNotifiesDeveloperWithoutAddingOrReactivatingMembership(InternalMailRelatedType type)
    {
        var user = new User("SaleUser");
        var store = CreateStore(user, type, true);
        var memory = (MemoryStore)(object)store.Db;
        var fixture = ObserverParticipantFixture(memory, user.CompanyId!.Value);
        var developer = fixture[0].EmployeeId;
        var old = new InternalConversationParticipant { InternalConversationId = store.Child.InternalConversationId,
            EmployeeId = developer, IsActive = false, IsMuted = true, IsArchived = true };
        memory.Values["InternalConversationParticipants"] = new MemorySet<InternalConversationParticipant>([old]);
        var notifications = new List<Notification>();
        var recipients = new List<NotificationRecipient>();
        var states = new List<NotificationUserState>();
        var outbox = new List<OutboxMessage>();
        memory.Values["Notifications"] = new SendRecordingSet<Notification>(notifications);
        memory.Values["NotificationRecipients"] = new SendRecordingSet<NotificationRecipient>(recipients);
        memory.Values["NotificationUserStates"] = new SendRecordingSet<NotificationUserState>(states);
        memory.Values["OutboxMessages"] = new SendRecordingSet<OutboxMessage>(outbox);
        memory.Values["SaveChangesAsync"] = Task.FromResult(1);
        var notificationDb = DispatchProxy.Create<INotificationDbContext, MemoryStore>();
        var proxy = (MemoryStore)(object)notificationDb;
        foreach (var entry in memory.Values) proxy.Values[entry.Key] = entry.Value;
        var access = new InternalMailAreaAccessService(store.Db, user);
        var service = new NotificationService(notificationDb, user, new ObserverPushEnabled(), null!, access, null!);
        var id = await service.PublishAsync(new PublishNotificationRequest {
            CompanyId = user.CompanyId, CreatedBy = user.EmployeeId, ConversationId = store.Child.InternalConversationId,
            UseExactAudience = true, TargetUserIds = [], SilentUserIds = [developer],
            Topic = TopicNotifications.SampleRequestMessageCreated, Title = "Synthetic observer notification"
        });
        Assert.Single(notifications);
        Assert.Equal(developer, Assert.Single(recipients).TargetUserId);
        var state = Assert.Single(states);
        Assert.Equal(id, state.NotificationId);
        Assert.Equal(developer, state.UserId);
        Assert.False(state.IsRead);
        Assert.False(state.IsArchived);
        Assert.Equal(2, outbox.Count); // In-app and Web Push, never a direct transport call.
        Assert.All(outbox, item => Assert.Contains(developer.ToString(), item.PayloadJson));
        Assert.False(old.IsActive);
        Assert.True(old.IsMuted);
        Assert.True(old.IsArchived);
        Assert.Single(store.Child.Participants);
        memory.Values["QueryInternalMailNotificationLinks"] = new[] {
            new HRM.Application.Abstractions.Persistence.InternalMail.InternalMailNotificationLink {
                NotificationId = id, HasConversation = true, ConversationId = store.Child.InternalConversationId }
        }.AsQueryable();
        Assert.Equal(new[] { developer }, access.DeliveryRecipients(notifications.Single()).ToArray());
        old.IsActive = true; // Even a muted historical Dev participant receives observer delivery.
        Assert.Equal(new[] { developer }, access.DeliveryRecipients(notifications.Single()).ToArray());
        store.Child.IsActive = false;
        Assert.Empty(access.DeliveryRecipients(notifications.Single()));
        // Role revocation, inactive employees and cross-company moves are checked again at dispatch/publication.
        store.Child.IsActive = true;
        var developerEmployee = store.Db.Employees.Single(e => e.EmployeeId == developer);
        var developerRole = store.Db.UserRoles.Single();
        foreach (var boundary in new[] { "inactive-employee", "inactive-role", "other-company", "inactive-conversation", "inactive-parent" })
        {
            developerEmployee.IsActive = boundary != "inactive-employee";
            developerEmployee.CompanyId = boundary == "other-company" ? Guid.NewGuid() : user.CompanyId.Value;
            developerRole.IsActive = boundary != "inactive-role";
            store.Child.IsActive = boundary != "inactive-conversation";
            store.Root.IsActive = boundary != "inactive-parent";
            if (boundary != "inactive-parent" || InternalMailAreaAccessService.IsPrivateArea(type))
            {
                Assert.Empty(access.DeliveryRecipients(notifications.First()));
                var before = states.Count;
                await service.PublishAsync(new() { CompanyId = user.CompanyId, CreatedBy = user.EmployeeId,
                    ConversationId = store.Child.InternalConversationId, UseExactAudience = true,
                    Topic = TopicNotifications.SampleRequestMessageCreated });
                Assert.Equal(before, states.Count);
            }
        }
    }

    [Theory]
    [InlineData("President", false)]
    [InlineData("SaleUser", false)]
    [InlineData("Developer", true)]
    public void ExecutiveReadBypassIsDeveloperOnly(string role, bool expected)
        => Assert.Equal(expected, InternalConversationAccessRules.CanReadExecutiveSampleRequestConversations(new User(role)));

    [Theory]
    [InlineData("Developer", true)]
    [InlineData("President", false)]
    [InlineData("SaleUser", false)]
    [InlineData("LabUser", false)]
    public void UnreadMembershipExceptionComesOnlyFromServerDeveloperDecision(string role, bool expected)
    {
        var user = new User(role) { HasExplicitPermissionSet = true,
            Permissions = [HRM.Application.Commons.Authorization.ApplicationPermissions.InternalMail.ObserveCompanyConversations] };
        var db = DispatchProxy.Create<HRM.Application.Abstractions.Persistence.InternalMail.IInternalMailDbContext, UnreadObserverDecisionRecorder>();
        _ = new InternalMailAreaAccessService(db, user).NotificationUnreadCounts();
        Assert.Equal(expected, ((UnreadObserverDecisionRecorder)(object)db).ObserverDecision);
    }

    public class UnreadObserverDecisionRecorder : MemoryStore
    {
        public bool? ObserverDecision { get; private set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == "QueryInternalConversationNotificationUnreadCounts")
            {
                ObserverDecision = (bool)args![3]!;
                return new AsyncQuery<HRM.Application.Abstractions.Persistence.InternalMail.InternalConversationNotificationUnreadCount>([]);
            }
            return base.Invoke(method, args);
        }
    }

    [Theory]
    [InlineData("President")]
    [InlineData("SaleUser")]
    public async Task LegacySampleChatCannotCreateItsOwnMembershipToBypassSendScope(string role)
    {
        var user = new User(role);
        var store = CreateStore(user, InternalMailRelatedType.SampleRequest, false);
        var sampleId = Guid.NewGuid();
        store.Child.RelatedId = sampleId;
        var memory = (MemoryStore)(object)store.Db;
        memory.Values["SampleRequests"] = new MemorySet<HRM.Domain.Entities.SampleRequestSchema.SampleRequest>([new() {
            SampleRequestId = sampleId, CompanyId = user.CompanyId!.Value, IsActive = true,
            Customer = new() { ExternalId = "SYNTHETIC_CUSTOMER" }, Product = new()
        }]);
        var plm = DispatchProxy.Create<HRM.Application.Abstractions.Persistence.PLM.IPLMWriteDbContext, MemoryStore>();
        foreach (var entry in memory.Values) ((MemoryStore)(object)plm).Values[entry.Key] = entry.Value;
        var handler = new HRM.Application.Features.PLM.SampleRequests.Commands.SendSampleRequestMessage.SendSampleRequestMessageCommandHandler(
            plm, user, null!, null!, new(store.Db, user), null!, null!);
        var result = await handler.Handle(new() { SampleRequestId = sampleId, Message = "Synthetic message",
            RequireExistingConversationSendAccess = true }, default);
        Assert.False(result.Success);
        Assert.Equal("Conversation was not found.", result.Message);
        Assert.Empty(store.Child.Participants);
        Assert.Empty(store.Db.InternalConversationParticipants);
    }

    private sealed class ObserverPushEnabled : IWebPushSender
    {
        public bool IsEnabled => true;
        public string PublicKey => "";
        public Task<WebPushSendResult> SendAsync(WebPushSubscriptionData subscription, WebPushPayload payload,
            CancellationToken cancellationToken = default) => throw new InvalidOperationException("Publish must only enqueue outbox.");
    }
}
