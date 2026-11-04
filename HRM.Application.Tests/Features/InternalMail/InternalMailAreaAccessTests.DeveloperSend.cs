using System.Reflection;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.InternalMail.Commands.SendMessage;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Application.Features.InternalMail.Services;
using HRM.Application.Features.Notifications.Services;
using HRM.Application.Features.Notifications.Dtos;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Enums.InternalMailEnums;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Xunit;

namespace HRM.Application.Tests.Features.InternalMail;

public sealed partial class InternalMailAreaAccessTests
{
    [Theory]
    [InlineData(InternalMailRelatedType.ConversationTechnical, false)]
    [InlineData(InternalMailRelatedType.ConversationTechnical, true)]
    [InlineData(InternalMailRelatedType.ConversationPricing, true)]
    [InlineData(InternalMailRelatedType.ConversationCustom, true)]
    public async Task PrivateChatNotifiesActiveMembersWithoutMentionEvenWhenMuted(InternalMailRelatedType type, bool muted)
    {
        var user = new User("Developer");
        var store = CreateStore(user, type, false);
        var recipient = Guid.NewGuid();
        var member = new InternalConversationParticipant
        {
            InternalConversationId = store.Child.InternalConversationId,
            EmployeeId = recipient, IsActive = true, IsMuted = muted
        };
        store.Child.Participants.Add(member);
        var memory = (MemoryStore)(object)store.Db;
        memory.Values[nameof(store.Db.Employees)] = new MemorySet<HRM.Domain.Entities.HrSchema.Employee>([
            new() { EmployeeId = recipient, CompanyId = user.CompanyId!.Value, IsActive = true }
        ]);
        memory.Values[nameof(store.Db.InternalConversationParticipants)] = new MemorySet<InternalConversationParticipant>([member]);
        memory.Values[nameof(store.Db.InternalMessages)] = new SendRecordingSet<InternalMessage>([]);
        memory.Values[nameof(store.Db.InternalMessageReadStates)] = new SendRecordingSet<InternalMessageReadState>([]);
        memory.Values[nameof(store.Db.SaveChangesAsync)] = Task.FromResult(1);
        var notifications = DispatchProxy.Create<INotificationService, SendNotificationRecorder>();
        var access = new InternalMailAreaAccessService(store.Db, user);
        var handler = new SendInternalMessageCommandHandler(store.Db, user, new SendTestClock(), notifications,
            null!, null!, access, new InternalMailMentionResolver(store.Db, access), null!);
        var result = await handler.Handle(new() { ConversationId = store.Child.InternalConversationId, Body = "Chào em" }, default);
        Assert.True(result.Success, result.Message);
        var published = ((SendNotificationRecorder)(object)notifications).Request!;
        Assert.True(published.KeepMutedPrivateChatUnread);
        Assert.Contains(recipient, published.TargetUserIds!);
        Assert.DoesNotContain(user.EmployeeId!.Value, published.TargetUserIds!);
        member.IsActive = false;
        result = await handler.Handle(new() { ConversationId = store.Child.InternalConversationId, Body = "Tin tiếp theo" }, default);
        Assert.True(result.Success, result.Message);
        Assert.Empty(((SendNotificationRecorder)(object)notifications).Request!.TargetUserIds!);
    }

    [Theory]
    [InlineData(InternalMailRelatedType.Internal)]
    [InlineData(InternalMailRelatedType.SampleRequest)]
    [InlineData(InternalMailRelatedType.Quotation)]
    [InlineData(InternalMailRelatedType.ConversationTechnical)]
    [InlineData(InternalMailRelatedType.ConversationPricing)]
    [InlineData(InternalMailRelatedType.ConversationCustom)]
    public async Task DeveloperCanSendInEveryVisibleAreaWithoutMembership(InternalMailRelatedType type)
    {
        var user = new User("Developer") { HasExplicitPermissionSet = true, Permissions = [] };
        var store = CreateStore(user, type, false);
        var access = new InternalMailAreaAccessService(store.Db, user);
        Assert.Contains(access.SendableConversations(), c => c.InternalConversationId == store.Child.InternalConversationId);
        ((MemoryStore)(object)store.Db).Values[nameof(store.Db.QueryInternalConversationNotificationUnreadCounts)] =
            new AsyncQuery<HRM.Application.Abstractions.Persistence.InternalMail.InternalConversationNotificationUnreadCount>([]);
        var dto = new InternalConversationDetailDto
        {
            ConversationId = store.Child.InternalConversationId, RelatedId = store.Child.RelatedId,
            RelatedType = type, Participants = []
        };
        var areas = await access.DescribeAsync(dto, default);
        Assert.All(areas.Where(a => a.ConversationId.HasValue), area => Assert.True(area.CanSend));
        Assert.Empty(store.Child.Participants);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("SaleUser")]
    [InlineData("LabUser")]
    public void CompanySendClaimDoesNotReplaceMembershipForOtherRoles(string role)
    {
        var user = new User(role) { HasExplicitPermissionSet = true, Permissions = [ApplicationPermissions.InternalMail.SendCompanyMessages] };
        var store = CreateStore(user, InternalMailRelatedType.ConversationCustom, false);
        var access = new InternalMailAreaAccessService(store.Db, user);
        Assert.False(access.CanSendCompanyMessages);
        Assert.Empty(access.SendableConversations());
    }

    [Theory]
    [InlineData(InternalMailRelatedType.SampleRequest, false)]
    [InlineData(InternalMailRelatedType.SampleRequest, true)]
    [InlineData(InternalMailRelatedType.ConversationTechnical, false)]
    [InlineData(InternalMailRelatedType.ConversationTechnical, true)]
    [InlineData(InternalMailRelatedType.ConversationPricing, false)]
    [InlineData(InternalMailRelatedType.ConversationPricing, true)]
    [InlineData(InternalMailRelatedType.ConversationCustom, false)]
    [InlineData(InternalMailRelatedType.ConversationCustom, true)]
    public Task PresidentCannotReadOrSendSampleAreaWithoutMembershipEvenWithLegacyClaims(InternalMailRelatedType type, bool explicitToken)
    {
        var user = new User("President") { HasExplicitPermissionSet = explicitToken,
            Permissions = [ApplicationPermissions.InternalMail.ObserveSampleRequestGroups,
                ApplicationPermissions.InternalMail.SendCompanyMessages, ApplicationPermissions.InternalMail.ObserveCompanyConversations] };
        var store = CreateStore(user, type, false);
        store.Root.RelatedType = InternalMailRelatedType.SampleRequest;
        var access = new InternalMailAreaAccessService(store.Db, user);
        Assert.Empty(access.Conversations());
        Assert.Empty(access.SendableConversations());
        Assert.False(access.CanSendCompanyMessages);
        store.Child.Participants.Add(new() { EmployeeId = user.EmployeeId!.Value, IsActive = true });
        Assert.Contains(access.SendableConversations(), c => c.InternalConversationId == store.Child.InternalConversationId);
        store.Child.Participants.Single().IsActive = false;
        Assert.Empty(access.SendableConversations());
        return Task.CompletedTask;
    }

    [Fact]
    public void PresidentSendCapabilityDoesNotOpenUnreadableConversations()
    {
        var user = new User("President");
        var store = CreateStore(user, InternalMailRelatedType.ConversationCustom, false);
        var access = new InternalMailAreaAccessService(store.Db, user);
        Assert.False(access.CanSendCompanyMessages);
        Assert.DoesNotContain(access.SendableConversations(), c => c.InternalConversationId == store.Child.InternalConversationId);
        store.Root.RelatedType = InternalMailRelatedType.SampleRequest;
        store.Child.CompanyId = Guid.NewGuid();
        Assert.DoesNotContain(access.SendableConversations(), c => c.InternalConversationId == store.Child.InternalConversationId);
        store.Child.CompanyId = user.CompanyId!.Value;
        store.Root.IsActive = false;
        Assert.DoesNotContain(access.SendableConversations(), c => c.InternalConversationId == store.Child.InternalConversationId);
    }

    [Theory]
    [InlineData("foreign")]
    [InlineData("inactive")]
    [InlineData("inactive-parent")]
    [InlineData("anonymous")]
    [InlineData("no-company")]
    public void DeveloperSendPreservesCompanyAndActiveScope(string boundary)
    {
        var user = new User("Developer");
        var store = CreateStore(user, InternalMailRelatedType.ConversationPricing, false);
        if (boundary == "foreign") store.Child.CompanyId = Guid.NewGuid();
        if (boundary == "inactive") store.Child.IsActive = false;
        if (boundary == "inactive-parent") store.Root.IsActive = false;
        if (boundary == "anonymous") user.IsAuthenticated = false;
        if (boundary == "no-company") user.CompanyId = null;
        Assert.DoesNotContain(new InternalMailAreaAccessService(store.Db, user).SendableConversations(),
            c => c.InternalConversationId == store.Child.InternalConversationId);
    }

    [Theory]
    [InlineData("Developer")]
    public async Task PrivilegedSendHandlerSavesMessageWithoutRequiringOrCreatingSenderMembership(string role)
    {
        var user = new User(role);
        var store = CreateStore(user, InternalMailRelatedType.ConversationPricing, false);
        if (role == "President") store.Root.RelatedType = InternalMailRelatedType.SampleRequest;
        var memory = (MemoryStore)(object)store.Db;
        var saved = new List<InternalMessage>();
        memory.Values[nameof(store.Db.InternalMessages)] = new SendRecordingSet<InternalMessage>(saved);
        memory.Values[nameof(store.Db.SaveChangesAsync)] = Task.FromResult(1);
        var notifications = DispatchProxy.Create<INotificationService, SendNotificationRecorder>();
        var access = new InternalMailAreaAccessService(store.Db, user);
        var handler = new SendInternalMessageCommandHandler(store.Db, user, new SendTestClock(), notifications,
            null!, null!, access, new InternalMailMentionResolver(store.Db, access), null!);
        var result = await handler.Handle(new()
        {
            ConversationId = store.Child.InternalConversationId, AreaCode = "pricing", Body = "Developer message"
        }, default);
        Assert.True(result.Success, result.Message);
        var message = Assert.Single(saved);
        Assert.Equal(user.EmployeeId, message.SenderEmployeeId);
        Assert.Equal("Developer message", message.Body);
        Assert.Equal(message.InternalMessageId, store.Child.LastMessageId);
        Assert.Empty(store.Child.Participants);
        Assert.Empty(store.Db.InternalConversationParticipants);
        var published = ((SendNotificationRecorder)(object)notifications).Request;
        Assert.True(published!.UseExactAudience);
        Assert.Equal(user.EmployeeId, published.CreatedBy);
    }

    public class SendNotificationRecorder : DispatchProxy
    {
        public PublishNotificationRequest? Request { get; private set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name != nameof(INotificationService.PublishAsync)) throw new NotSupportedException(method.Name);
            Request = (PublishNotificationRequest)args![0]!;
            return Task.FromResult(Guid.Empty);
        }
    }

    private sealed class SendTestClock : IDateTimeProvider
    {
        public DateTime Now => new(2026, 10, 30, 0, 0, 0, DateTimeKind.Utc);
    }

    private sealed class SendRecordingSet<T>(List<T> items) : MemorySet<T>(items) where T : class
    {
        public override ValueTask<EntityEntry<T>> AddAsync(T entity, CancellationToken cancellationToken = default)
        {
            items.Add(entity);
            return ValueTask.FromResult<EntityEntry<T>>(null!);
        }
    }
}
