using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.InternalMail.Services;
using HRM.Application.Features.InternalMail.Queries.GetConversationDetail;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Entities.Notifications;
using HRM.Domain.Enums.InternalMailEnums;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.InternalMail;

public sealed class InternalMailAreaAccessTests
{
    [Theory]
    [InlineData("SaleUser", InternalMailRelatedType.ConversationTechnical, true)]
    [InlineData("LabUser", InternalMailRelatedType.ConversationPricing, true)]
    [InlineData("President", InternalMailRelatedType.ConversationTechnical, false)]
    [InlineData("Developer", InternalMailRelatedType.ConversationPricing, false)]
    [InlineData("SaleUser", InternalMailRelatedType.ConversationTechnical, false)]
    public void PrivateArea_ReadAccessDependsOnMembership_NotDepartmentRole(string role, InternalMailRelatedType type, bool member)
    {
        var user = new User(role);
        var store = CreateStore(user, type, member);
        var access = new InternalMailAreaAccessService(store.Db, user);
        Assert.Equal(member, access.Conversations().Any(c => c.InternalConversationId == store.Child.InternalConversationId));
        Assert.Equal(member, access.Messages().Any());
        Assert.Empty(access.Messages("unknown"));
        Assert.Empty(access.Messages(type == InternalMailRelatedType.ConversationTechnical ? "pricing" : "technical"));
    }

    [Fact]
    public void ParentOwnerDoesNotInheritPrivateHistory_AndRevocationIsImmediate()
    {
        var user = new User("President");
        var store = CreateStore(user, InternalMailRelatedType.ConversationTechnical, false);
        store.Root.Participants.Add(new() { EmployeeId = user.EmployeeId!.Value, IsActive = true, Role = InternalConversationParticipantRole.Owner });
        var access = new InternalMailAreaAccessService(store.Db, user);
        Assert.Empty(access.Messages());
        var participant = new InternalConversationParticipant { EmployeeId = user.EmployeeId.Value, IsActive = true };
        store.Child.Participants.Add(participant);
        Assert.Single(access.Messages());
        participant.IsActive = false;
        Assert.Empty(access.Messages());
    }

    [Fact]
    public void MembershipDoesNotBypassTenant_OrInactiveParent()
    {
        var user = new User("SaleUser");
        var store = CreateStore(user, InternalMailRelatedType.ConversationPricing, true);
        var access = new InternalMailAreaAccessService(store.Db, user);
        Assert.Single(access.Messages());
        store.Root.IsActive = false;
        Assert.Empty(access.Messages());
        store.Root.IsActive = true;
        user.CompanyId = Guid.NewGuid();
        Assert.Empty(access.Messages());
    }

    [Fact]
    public void AnonymousUserCannotReadEvenWithMembership()
    {
        var user = new User("SaleUser") { IsAuthenticated = false };
        var store = CreateStore(user, InternalMailRelatedType.ConversationTechnical, true);
        Assert.Empty(new InternalMailAreaAccessService(store.Db, user).Messages());
    }

    [Fact]
    public void HttpPayloadCannotSetTrustedForwardFlag()
    {
        var command = System.Text.Json.JsonSerializer.Deserialize<HRM.Application.Features.InternalMail.Commands.SendMessage.SendInternalMessageCommand>(
            "{\"Body\":\"text\",\"IsForwarded\":true}")!;
        var flag = command.GetType().GetProperty("IsForwarded", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.Equal(false, flag.GetValue(command));
    }

    [Fact]
    public void GenericAttachmentRoutesCannotBypassPrivateMembership()
    {
        var user = new User("SaleUser");
        var store = CreateStore(user, InternalMailRelatedType.ConversationTechnical, true);
        var message = store.Db.InternalMessages.First();
        var attachment = new HRM.Domain.Entities.AttachmentSchema.AttachmentModel
        {
            Slot = HRM.Domain.Enums.Attachment.AttachmentSlot.InternalMail,
            InternalMessageAttachments = [new() { InternalMessageId = message.InternalMessageId }]
        };
        var access = new InternalMailAreaAccessService(store.Db, user);
        var source = new[] { attachment }.AsQueryable();
        Assert.Single(access.Attachments(source));
        message.IsDeleted = true;
        Assert.Empty(access.Attachments(source));
        message.IsDeleted = false;
        store.Child.Participants.Single().IsActive = false;
        Assert.Empty(access.Attachments(source));
        attachment.InternalMessageAttachments.Clear();
        Assert.Empty(access.Attachments(source));
    }

    [Fact]
    public void QueriesTranslateOnPostgres_WithoutNewEntityColumnsOrRoleClaims()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var user = new User("SaleUser");
        var access = new InternalMailAreaAccessService(db, user);
        var sql = access.Messages("technical").Select(m => m.InternalMessageId).ToQueryString();
        Assert.Contains("InternalConversationParticipants", sql);
        Assert.Contains("CompanyId", sql);
        Assert.Contains("QAQC.RD", sql);
        Assert.DoesNotContain("AspNetRoleClaims", sql);
        Assert.Null(db.Model.FindEntityType(typeof(InternalMessage))!.FindProperty("AreaCode"));
        Assert.Null(db.Model.FindEntityType(typeof(Notification))!.FindProperty("ConversationId"));
        Assert.NotEmpty(access.States().Select(s => s.NotificationId).ToQueryString());
        Assert.NotEmpty(access.NotificationUnreadCounts().ToQueryString());
        Assert.NotEmpty(access.Attachments(db.AttachmentModels).ToQueryString());
        Assert.NotEmpty(access.Conversations().Select(InternalConversationDetailProjection.ForEmployee(user.EmployeeId!.Value, access.Messages())).ToQueryString());
        Assert.NotEmpty(access.DeliveryRecipients(new Notification { Id = Guid.NewGuid(), CompanyId = user.CompanyId!.Value }).ToQueryString());
    }

    private static (IInternalMailDbContext Db, InternalConversation Root, InternalConversation Child) CreateStore(User user, InternalMailRelatedType type, bool member)
    {
        var root = new InternalConversation { InternalConversationId = Guid.NewGuid(), CompanyId = user.CompanyId!.Value, RelatedType = InternalMailRelatedType.Internal, IsActive = true };
        var child = new InternalConversation { InternalConversationId = Guid.NewGuid(), CompanyId = user.CompanyId.Value, RelatedType = type, RelatedId = root.InternalConversationId, IsActive = true };
        if (member) child.Participants.Add(new() { EmployeeId = user.EmployeeId!.Value, IsActive = true });
        var db = DispatchProxy.Create<IInternalMailDbContext, MemoryStore>();
        var proxy = (MemoryStore)(object)db;
        proxy.Values[nameof(db.InternalConversations)] = new MemorySet<InternalConversation>([root, child]);
        proxy.Values[nameof(db.InternalMessages)] = new MemorySet<InternalMessage>([new() { InternalMessageId = Guid.NewGuid(), InternalConversationId = child.InternalConversationId, Conversation = child }]);
        return (db, root, child);
    }

    public class MemoryStore : DispatchProxy
    {
        public Dictionary<string, object> Values { get; } = [];
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name.StartsWith("get_"))
            {
                var name = method.Name[4..];
                if (Values.TryGetValue(name, out var value)) return value;
                var type = method.ReturnType.GetGenericArguments()[0];
                return Activator.CreateInstance(typeof(MemorySet<>).MakeGenericType(type), [Array.CreateInstance(type, 0)]);
            }
            throw new NotSupportedException(method.Name);
        }
    }

    public class MemorySet<T>(IEnumerable<T> items) : DbSet<T>, IQueryable<T> where T : class
    {
        public override Microsoft.EntityFrameworkCore.Metadata.IEntityType EntityType => throw new NotSupportedException();
        private readonly IQueryable<T> _query = items.AsQueryable();
        Type IQueryable.ElementType => typeof(T);
        Expression IQueryable.Expression => _query.Expression;
        IQueryProvider IQueryable.Provider => _query.Provider;
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => _query.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _query.GetEnumerator();
    }

    private sealed class User(string role) : ICurrentUser
    {
        public bool IsAuthenticated { get; set; } = true;
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid? EmployeeId { get; } = Guid.NewGuid();
        public Guid? CompanyId { get; set; } = Guid.NewGuid();
        public string? UserName => "test";
        public string? Email => null;
        public IReadOnlyCollection<string> Roles => [role];
        public bool IsInRole(string value) => value == role;
    }
}
