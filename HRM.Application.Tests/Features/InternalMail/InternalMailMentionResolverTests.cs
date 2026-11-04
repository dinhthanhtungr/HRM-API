using System.Reflection;
using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.InternalMail.Services;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Enums.InternalMailEnums;
using HRM.Domain.Entities.Notifications;
using System.Text.Json;

namespace HRM.Application.Tests.Features.InternalMail;

public sealed class InternalMailMentionResolverTests
{
    [Theory]
    [InlineData(true, true, true, true)]
    [InlineData(false, true, true, false)]
    [InlineData(true, false, true, false)]
    [InlineData(true, true, false, false)]
    public async Task MentionRequiresActiveMembershipAndCompanyButAllowsMutedMembers(
        bool activeMembership, bool activeEmployee, bool sameCompany, bool expected)
    {
        var user = new User();
        var employee = new Employee
        {
            EmployeeId = Guid.NewGuid(), FullName = "Lab Nam", IsActive = activeEmployee,
            CompanyId = sameCompany ? user.CompanyId!.Value : Guid.NewGuid()
        };
        var conversation = new InternalConversation
        {
            InternalConversationId = Guid.NewGuid(), CompanyId = user.CompanyId!.Value, IsActive = true,
            RelatedType = InternalMailRelatedType.Internal
        };
        var participant = new InternalConversationParticipant
        {
            InternalConversationId = conversation.InternalConversationId, Conversation = conversation,
            EmployeeId = employee.EmployeeId, IsActive = activeMembership, IsMuted = true
        };
        var db = DispatchProxy.Create<IInternalMailDbContext, InternalMailAreaAccessTests.MemoryStore>();
        var values = ((InternalMailAreaAccessTests.MemoryStore)(object)db).Values;
        values["InternalConversations"] = new InternalMailAreaAccessTests.MemorySet<InternalConversation>([conversation]);
        values["InternalConversationParticipants"] = new InternalMailAreaAccessTests.MemorySet<InternalConversationParticipant>([participant]);
        values["Employees"] = new InternalMailAreaAccessTests.MemorySet<Employee>([employee]);
        var resolver = new InternalMailMentionResolver(db, new(db, user));
        var result = await resolver.ResolveAsync(user.CompanyId.Value, conversation.InternalConversationId, [employee.EmployeeId], default);
        Assert.Equal(expected, result.Success);
        Assert.False((await resolver.ResolveAsync(user.CompanyId.Value, Guid.NewGuid(), [employee.EmployeeId], default)).Success);
        Assert.False((await resolver.ResolveAsync(user.CompanyId.Value, conversation.InternalConversationId, [Guid.Empty], default)).Success);
    }

    [Theory]
    [InlineData(true, true, true, false, true)]
    [InlineData(false, true, true, false, false)]
    [InlineData(true, true, false, false, false)]
    [InlineData(true, true, true, true, false)]
    public void DispatchMentionBypassesMuteButNotRevocationOrArchivedInbox(
        bool mention, bool muted, bool member, bool archived, bool expected)
    {
        var user = new User();
        var conversation = new InternalConversation
        {
            InternalConversationId = Guid.NewGuid(), CompanyId = user.CompanyId!.Value, IsActive = true,
            RelatedType = InternalMailRelatedType.Internal
        };
        conversation.Participants.Add(new InternalConversationParticipant
        {
            EmployeeId = user.EmployeeId!.Value, IsActive = member, IsMuted = muted
        });
        var notification = new Notification
        {
            Id = Guid.NewGuid(), CompanyId = user.CompanyId.Value,
            PayloadJson = JsonSerializer.Serialize(new { deliveryMentionEmployeeIds = mention ? new[] { user.EmployeeId.Value } : [] })
        };
        var db = DispatchProxy.Create<IInternalMailDbContext, InternalMailAreaAccessTests.MemoryStore>();
        var values = ((InternalMailAreaAccessTests.MemoryStore)(object)db).Values;
        values["InternalConversations"] = new InternalMailAreaAccessTests.MemorySet<InternalConversation>([conversation]);
        values["Employees"] = new InternalMailAreaAccessTests.MemorySet<Employee>([
            new Employee { EmployeeId = user.EmployeeId.Value, CompanyId = user.CompanyId.Value, IsActive = true }]);
        values["NotificationUserStates"] = new InternalMailAreaAccessTests.MemorySet<NotificationUserState>([
            new NotificationUserState { NotificationId = notification.Id, UserId = user.EmployeeId.Value, IsArchived = archived }]);
        values["QueryInternalMailNotificationLinks"] = new[] { new InternalMailNotificationLink
        {
            NotificationId = notification.Id, HasConversation = true, ConversationId = conversation.InternalConversationId
        } }.AsQueryable();
        Assert.Equal(expected, new InternalMailAreaAccessService(db, user).DeliveryRecipients(notification).Any());
    }

    private sealed class User : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid? EmployeeId { get; } = Guid.NewGuid();
        public Guid? CompanyId { get; } = Guid.NewGuid();
        public string? UserName => "test";
        public string? Email => null;
        public IReadOnlyCollection<string> Roles => [];
        public bool IsInRole(string role) => false;
    }
}
