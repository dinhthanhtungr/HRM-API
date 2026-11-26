using HRM.Application.Features.InternalMail.Services;
using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Domain.Enums.InternalMailEnums;
namespace HRM.Application.Tests.Features.InternalMail;
public sealed partial class InternalMailAreaAccessTests
{
    [Theory]
    [InlineData("LabUser")]
    [InlineData("SaleUser")]
    public async Task SampleWorkUnreadAggregatesVisibleGroupsAndRechecksCompany(string role)
    {
        var user = new User(role);
        var store = CreateStore(user, InternalMailRelatedType.ConversationTechnical, true);
        var requestId = Guid.NewGuid();
        store.Root.RelatedType = InternalMailRelatedType.SampleRequest;
        store.Root.RelatedId = requestId;
        store.Root.Participants.Add(new() { EmployeeId = user.EmployeeId!.Value, IsActive = true });
        ((MemoryStore)(object)store.Db).Values[nameof(store.Db.QueryInternalConversationNotificationUnreadCounts)] =
            new AsyncQuery<InternalConversationNotificationUnreadCount>([
                new() { ConversationId = store.Root.InternalConversationId, UnreadCount = 2 },
                new() { ConversationId = store.Child.InternalConversationId, UnreadCount = 3 }
            ]);
        var access = new InternalMailAreaAccessService(store.Db, user);
        var result = await access.SampleWorkUnreadAsync([requestId, requestId, Guid.NewGuid()], default);
        Assert.Equal(5, Assert.Single(result).Value);
        Assert.True(result.ContainsKey(requestId));
        user.CompanyId = Guid.NewGuid();
        Assert.Empty(await access.SampleWorkUnreadAsync([requestId], default));
    }

    [Fact]
    public async Task SampleWorkUnreadRejectsUnboundedBatchesAndEmptyScope()
    {
        var user = new User("LabUser");
        var store = CreateStore(user, InternalMailRelatedType.Internal, true);
        var access = new InternalMailAreaAccessService(store.Db, user);
        Assert.Empty(await access.SampleWorkUnreadAsync([], default));
        await Assert.ThrowsAsync<ArgumentException>(() => access.SampleWorkUnreadAsync(new Guid[101], default));
        user.CompanyId = null;
        Assert.Empty(await access.SampleWorkUnreadAsync([Guid.NewGuid()], default));
    }
}
