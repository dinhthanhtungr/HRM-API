using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Features.InternalMail.Queries.GetConversationReadState;
using HRM.Application.Features.InternalMail.Services;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Enums.InternalMailEnums;

namespace HRM.Application.Tests.Features.InternalMail;

public sealed partial class InternalMailAreaAccessTests
{
    [Theory]
    [InlineData("LabUser")]
    [InlineData("SaleUser")]
    public async Task CompactReadStateReturnsOwnCanonicalCountsWithoutMetadataEnrichment(string role)
    {
        var user = new User(role);
        var store = CreateStore(user, InternalMailRelatedType.Internal, true);
        var message = store.Db.InternalMessages.Single();
        message.SentAt = new DateTime(2026, 11, 5, 8, 0, 0);
        ((MemoryStore)(object)store.Db).Values[nameof(store.Db.QueryInternalConversationNotificationUnreadCounts)] =
            new AsyncQuery<InternalConversationNotificationUnreadCount>([
                new() { ConversationId = store.Child.InternalConversationId, UnreadCount = 2 }
            ]);
        var handler = new GetInternalConversationReadStateQueryHandler(user, new InternalMailAreaAccessService(store.Db, user));
        var result = await handler.Handle(new() { ConversationId = store.Child.InternalConversationId,
            AreaCode = "general", ThroughMessageId = message.InternalMessageId }, default);
        Assert.NotNull(result);
        Assert.Equal(2, result.UnreadCount);
        Assert.Equal(message.InternalMessageId, result.LastMessageId);
        Assert.Equal(message.InternalMessageId, result.ThroughMessageId);
        Assert.Equal(2, Assert.Single(result.AreaUnreadCounts).UnreadCount);
    }

    [Theory]
    [InlineData(false, "general")]
    [InlineData(true, "pricing")]
    public async Task CompactReadStateRechecksMembershipAndArea(bool member, string area)
    {
        var user = new User("LabUser");
        var store = CreateStore(user, InternalMailRelatedType.Internal, member);
        var handler = new GetInternalConversationReadStateQueryHandler(user, new InternalMailAreaAccessService(store.Db, user));
        Assert.Null(await handler.Handle(new() { ConversationId = store.Child.InternalConversationId, AreaCode = area }, default));
    }

    [Fact]
    public async Task CompactReadStatePreservesUnreadNewerThanBoundaryAndExcludesUnjoinedSibling()
    {
        var user = new User("LabUser");
        var store = CreateStore(user, InternalMailRelatedType.ConversationTechnical, true);
        store.Root.Participants.Add(new() { EmployeeId = user.EmployeeId!.Value, IsActive = true });
        var boundary = store.Db.InternalMessages.Single();
        boundary.SentAt = new DateTime(2026, 11, 5, 8, 0, 0);
        var newer = new InternalMessage { InternalMessageId = Guid.NewGuid(), InternalConversationId = store.Child.InternalConversationId,
            Conversation = store.Child, SentAt = boundary.SentAt.AddMinutes(1) };
        var hidden = new InternalConversation { InternalConversationId = Guid.NewGuid(), CompanyId = user.CompanyId!.Value,
            RelatedType = InternalMailRelatedType.ConversationPricing, RelatedId = store.Root.InternalConversationId, IsActive = true };
        var memory = (MemoryStore)(object)store.Db;
        memory.Values[nameof(store.Db.InternalConversations)] = new MemorySet<InternalConversation>([store.Root, store.Child, hidden]);
        memory.Values[nameof(store.Db.InternalMessages)] = new MemorySet<InternalMessage>([boundary, newer]);
        memory.Values[nameof(store.Db.QueryInternalConversationNotificationUnreadCounts)] = new AsyncQuery<InternalConversationNotificationUnreadCount>([
            new() { ConversationId = store.Root.InternalConversationId, UnreadCount = 1 },
            new() { ConversationId = store.Child.InternalConversationId, UnreadCount = 2 },
            new() { ConversationId = hidden.InternalConversationId, UnreadCount = 9 },
        ]);
        var handler = new GetInternalConversationReadStateQueryHandler(user, new InternalMailAreaAccessService(store.Db, user));
        var result = await handler.Handle(new() { ConversationId = store.Child.InternalConversationId,
            AreaCode = "technical", ThroughMessageId = boundary.InternalMessageId }, default);
        Assert.NotNull(result);
        Assert.Equal(2, result.UnreadCount);
        Assert.Equal(newer.InternalMessageId, result.LastMessageId);
        Assert.Equal(boundary.InternalMessageId, result.ThroughMessageId);
        Assert.DoesNotContain(result.AreaUnreadCounts, area => area.ConversationId == hidden.InternalConversationId);
        Assert.Equal(2, result.AreaUnreadCounts.Count);
        user.CompanyId = Guid.NewGuid();
        Assert.Null(await handler.Handle(new() { ConversationId = store.Child.InternalConversationId }, default));
    }
}
