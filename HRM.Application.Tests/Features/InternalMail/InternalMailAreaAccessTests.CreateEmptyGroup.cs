using System.Reflection;
using HRM.Application.Features.InternalMail.Commands.ForwardToGroup;
using HRM.Application.Features.InternalMail.Services;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Enums.InternalMailEnums;
using MediatR;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace HRM.Application.Tests.Features.InternalMail;
public sealed partial class InternalMailAreaAccessTests
{
    [Theory]
    [InlineData("SaleUser", true, true, true)]
    [InlineData("SaleUser", false, true, false)]
    [InlineData("SaleUser", true, false, true)]
    [InlineData("Developer", false, true, true)]
    [InlineData("President", false, true, false)]
    [InlineData("Developer", false, false, true)]
    public async Task EmptyPrivateGroupRequiresSourceSendAccessAndActiveCompanyMembersWithoutCreatingMessages(string role, bool sourceMember, bool eligibleMember, bool expected)
    {
        var user = new User(role);
        var store = CreateStore(user, InternalMailRelatedType.Internal, false);
        var root = store.Root;
        root.RelatedType = InternalMailRelatedType.SampleRequest;
        root.RelatedId = Guid.NewGuid();
        root.CreatedAt = new DateTime(2026, 10, 1);
        var creator = new Employee { EmployeeId = user.EmployeeId!.Value, CompanyId = user.CompanyId!.Value, IsActive = true };
        var other = new Employee { EmployeeId = Guid.NewGuid(), CompanyId = user.CompanyId.Value, IsActive = true };
        if (sourceMember) root.Participants.Add(new() { InternalConversationId = root.InternalConversationId, EmployeeId = creator.EmployeeId, Employee = creator, Conversation = root, IsActive = true });
        if (eligibleMember) root.Participants.Add(new() { InternalConversationId = root.InternalConversationId, EmployeeId = other.EmployeeId, Employee = other, Conversation = root, IsActive = true });
        var memory = (MemoryStore)(object)store.Db;
        var conversations = new List<InternalConversation> { root, store.Child };
        memory.Values[nameof(store.Db.InternalConversations)] = new ActivityConversationSet(conversations);
        memory.Values[nameof(store.Db.InternalConversationParticipants)] = new MemorySet<InternalConversationParticipant>(root.Participants);
        memory.Values[nameof(store.Db.Employees)] = new MemorySet<Employee>([creator, other]);
        memory.Values[nameof(store.Db.InternalMessages)] = new MemorySet<InternalMessage>([]);
        memory.Values[nameof(store.Db.SaveChangesAsync)] = Task.FromResult(1);
        memory.Values[nameof(store.Db.LockInternalConversationAsync)] = Task.CompletedTask;
        memory.Values[nameof(store.Db.BeginTransactionAsync)] = Task.FromResult(DispatchProxy.Create<IDbContextTransaction, ReadReceiptTransaction>());
        var handler = new ForwardToInternalGroupCommandHandler(store.Db, user, new SendTestClock(),
            new InternalMailAreaAccessService(store.Db, user), DispatchProxy.Create<ISender, NoEmptyGroupForwarding>());
        var result = await handler.Handle(new() { ConversationId = root.InternalConversationId, Name = "Technical project", EmployeeIds = [other.EmployeeId], MessageIds = [] }, default);
        Assert.Equal(expected, result.Success);
        Assert.Equal(expected ? 3 : 2, conversations.Count);
        Assert.Empty(store.Db.InternalMessages);
        if (expected)
        {
            var group = conversations.Last();
            Assert.Equal("Technical project", group.Subject);
            Assert.Equal(root.InternalConversationId, group.RelatedId);
            Assert.Equal(root.CreatedAt, group.LastMessageAt);
            Assert.Equal(new SendTestClock().Now, group.CreatedAt);
            Assert.Equal(2, group.Participants.Count);
            Assert.Equal(eligibleMember, root.Participants.Any(p => p.EmployeeId == other.EmployeeId));
            var invited = new User("SaleUser") { EmployeeId = other.EmployeeId, CompanyId = user.CompanyId };
            var invitedAccess = new InternalMailAreaAccessService(store.Db, invited);
            Assert.Contains(invitedAccess.Conversations(), c => c.InternalConversationId == group.InternalConversationId);
            Assert.DoesNotContain(invitedAccess.Conversations(), c => c.InternalConversationId == root.InternalConversationId && !eligibleMember);
        }
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task EmptyGroupInvitationRejectsInactiveOrCrossCompanyEmployees(bool sameCompany, bool active)
    {
        var user = new User("President");
        var store = CreateStore(user, InternalMailRelatedType.Internal, false);
        var other = new Employee { EmployeeId = Guid.NewGuid(), CompanyId = sameCompany ? user.CompanyId!.Value : Guid.NewGuid(), IsActive = active };
        ((MemoryStore)(object)store.Db).Values[nameof(store.Db.Employees)] = new MemorySet<Employee>([other]);
        Assert.False(await new InternalMailAreaAccessService(store.Db, user).CanInviteToEmptyGroupAsync(store.Root.InternalConversationId, [other.EmployeeId], default));
    }
    public class NoEmptyGroupForwarding : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) => throw new InvalidOperationException("Empty group must not forward messages.");
    }
}
