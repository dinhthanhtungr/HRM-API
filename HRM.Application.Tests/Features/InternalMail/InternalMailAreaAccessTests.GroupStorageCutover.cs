using HRM.Application.Features.InternalMail.Services;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Enums.InternalMailEnums;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace HRM.Application.Tests.Features.InternalMail;

public sealed partial class InternalMailAreaAccessTests
{
    [Theory]
    [MemberData(nameof(ChatAccessCases))]
    public void GroupStorageCutoverSecurityMatrixMatchesLegacy(string role, InternalMailRelatedType area,
        bool member, bool sameCompany, bool activeRoot, bool activeGroup, bool authenticated)
    {
        var user = new User(role) { IsAuthenticated = authenticated };
        var store = CreateStore(user, area, member);
        store.Root.RelatedType = InternalMailRelatedType.SampleRequest;
        store.Root.IsActive = activeRoot;
        store.Child.IsActive = activeGroup;
        SeedMappings(store.Db);
        if (!sameCompany) user.CompanyId = Guid.NewGuid();
        var legacy = new InternalMailAreaAccessService(store.Db, user);
        var mapped = MappedAccess(store.Db, user);
        var expected = authenticated && sameCompany && activeRoot && activeGroup && (member || role == "Developer");
        Assert.Equal(expected, mapped.Conversations().Any(c => c.InternalConversationId == store.Child.InternalConversationId));
        Assert.Equal(legacy.SendableConversations().Select(c => c.InternalConversationId).ToArray(), mapped.SendableConversations().Select(c => c.InternalConversationId).ToArray());
        Assert.Equal(expected, mapped.Messages(InternalMailAreaAccessService.AreaOf(area)).Any());
        Assert.Equal(member ? 1 : 0, store.Child.Participants.Count);
    }

    [Theory]
    [InlineData("Developer", "technical", true)]
    [InlineData("Developer", "pricing", false)]
    [InlineData("President", "technical", false)]
    [InlineData("President", "pricing", false)]
    public async Task GroupStorageSendCommandPreservesAuthorAndRejectsWrongArea(string role, string area, bool expected)
    {
        var user = new User(role);
        var store = CreateStore(user, InternalMailRelatedType.ConversationTechnical, false);
        if (role == "President") store.Root.RelatedType = InternalMailRelatedType.SampleRequest;
        SeedMappings(store.Db);
        var saved = new List<InternalMessage>();
        var memory = (MemoryStore)(object)store.Db;
        memory.Values[nameof(store.Db.InternalMessages)] = new SendRecordingSet<InternalMessage>(saved);
        memory.Values[nameof(store.Db.SaveChangesAsync)] = Task.FromResult(1);
        var notifications = System.Reflection.DispatchProxy.Create<HRM.Application.Features.Notifications.Services.INotificationService, SendNotificationRecorder>();
        var access = MappedAccess(store.Db, user);
        var handler = new HRM.Application.Features.InternalMail.Commands.SendMessage.SendInternalMessageCommandHandler(
            store.Db, user, new SendTestClock(), notifications, null!, null!, access, new InternalMailMentionResolver(store.Db, access), null!);
        var response = await handler.Handle(new() { ConversationId = store.Child.InternalConversationId, AreaCode = area, Body = "Cutover test" }, default);
        Assert.Equal(expected, response.Success);
        if (expected)
        {
            var message = Assert.Single(saved);
            Assert.Equal(user.EmployeeId, message.SenderEmployeeId);
            Assert.Equal(message.InternalMessageId, store.Child.LastMessageId);
        }
        else Assert.Empty(saved);
        Assert.Empty(store.Child.Participants);
    }

    private static void SeedMappings(HRM.Application.Abstractions.Persistence.InternalMail.IInternalMailDbContext db)
    {
        var mappings = db.InternalConversations.Select(c => new InternalConversationGroup
        {
            ConversationId = c.InternalConversationId, CompanyId = c.CompanyId, IsActive = c.IsActive,
            RootConversationId = InternalMailAreaAccessService.IsPrivateArea(c.RelatedType) ? c.RelatedId!.Value : c.InternalConversationId,
            GroupType = c.RelatedType == InternalMailRelatedType.ConversationTechnical ? InternalConversationGroupType.Technical :
                c.RelatedType == InternalMailRelatedType.ConversationPricing ? InternalConversationGroupType.Pricing :
                c.RelatedType == InternalMailRelatedType.ConversationCustom ? InternalConversationGroupType.Custom : InternalConversationGroupType.General
        }).ToArray();
        ((MemoryStore)(object)db).Values[nameof(db.InternalConversationGroups)] = new MemorySet<InternalConversationGroup>(mappings);
    }

    private static InternalMailAreaAccessService MappedAccess(HRM.Application.Abstractions.Persistence.InternalMail.IInternalMailDbContext db, User user) =>
        new(db, user, Options.Create(new InternalMailGroupStorageOptions { Enabled = true }));

    [Theory]
    [InlineData("President", false)]
    [InlineData("Developer", false)]
    [InlineData("LabUser", false)]
    [InlineData("LabUser", true)]
    [InlineData("SaleUser", true)]
    [InlineData("PLPUUser", true)]
    [InlineData("PLPUUser", false)]
    public async Task GroupStorageCutoverPreservesVisibilitySendabilityMessagesAndRoots(string role, bool member)
    {
        foreach (var type in new[] { InternalMailRelatedType.ConversationTechnical, InternalMailRelatedType.ConversationPricing,
                     InternalMailRelatedType.ConversationCustom, InternalMailRelatedType.Internal, InternalMailRelatedType.Quotation })
        {
            var user = new User(role);
            var store = CreateStore(user, type, member);
            store.Root.RelatedType = InternalMailRelatedType.SampleRequest;
            SeedMappings(store.Db);
            var legacy = new InternalMailAreaAccessService(store.Db, user);
            var mapped = MappedAccess(store.Db, user);
            Assert.Equal(legacy.Conversations().Select(c => c.InternalConversationId).ToArray(), mapped.Conversations().Select(c => c.InternalConversationId).ToArray());
            Assert.Equal(legacy.SendableConversations().Select(c => c.InternalConversationId).ToArray(), mapped.SendableConversations().Select(c => c.InternalConversationId).ToArray());
            foreach (var area in new[] { null, "general", "technical", "pricing", "custom", "invalid" })
                Assert.Equal(legacy.Messages(area).Select(m => m.InternalMessageId).ToArray(), mapped.Messages(area).Select(m => m.InternalMessageId).ToArray());
            Assert.Equal(await legacy.RootIdAsync(store.Child.InternalConversationId, default), await mapped.RootIdAsync(store.Child.InternalConversationId, default));
        }
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("company")]
    [InlineData("inactive")]
    [InlineData("wrong-type")]
    [InlineData("wrong-root")]
    [InlineData("root-inactive")]
    [InlineData("root-type")]
    public void GroupStorageCutoverFailsClosedOnMissingOrInconsistentMapping(string failure)
    {
        var user = new User("Developer");
        var store = CreateStore(user, InternalMailRelatedType.ConversationTechnical, true);
        SeedMappings(store.Db);
        var mapping = store.Db.InternalConversationGroups.Single(g => g.ConversationId == store.Child.InternalConversationId);
        switch (failure)
        {
            case "missing": ((MemoryStore)(object)store.Db).Values[nameof(store.Db.InternalConversationGroups)] = new MemorySet<InternalConversationGroup>([]); break;
            case "company": mapping.CompanyId = Guid.NewGuid(); break;
            case "inactive": mapping.IsActive = false; break;
            case "wrong-type": mapping.GroupType = InternalConversationGroupType.Pricing; break;
            case "wrong-root": mapping.RootConversationId = Guid.NewGuid(); break;
            case "root-inactive": store.Root.IsActive = false; break;
            case "root-type": store.Db.InternalConversationGroups.Single(g => g.ConversationId == store.Root.InternalConversationId).GroupType = InternalConversationGroupType.Custom; break;
        }
        var access = MappedAccess(store.Db, user);
        Assert.DoesNotContain(access.Conversations(), c => c.InternalConversationId == store.Child.InternalConversationId);
        Assert.Empty(access.Messages());
    }

    [Fact]
    public async Task GroupStorageUsesStoredDisplayNameWithoutChangingActivityOrReadState()
    {
        var user = new User("Developer");
        var store = CreateStore(user, InternalMailRelatedType.ConversationTechnical, true);
        SeedMappings(store.Db);
        store.Db.InternalConversationGroups.Single(g => g.ConversationId == store.Child.InternalConversationId).DisplayName = "Thử màu đợt 2";
        var legacy = new InternalMailAreaAccessService(store.Db, user);
        var access = MappedAccess(store.Db, user);
        var before = store.Child.LastMessageAt;
        var group = (await access.GroupInfoAsync([store.Child.InternalConversationId], default))[store.Child.InternalConversationId];
        Assert.Equal("Thử màu đợt 2", group.DisplayName);
        Assert.Equal(legacy.InboxConversations().Select(c => c.ActivityAt).ToArray(), access.InboxConversations().Select(c => c.ActivityAt).ToArray());
        Assert.Equal(before, store.Child.LastMessageAt);
        Assert.Empty(store.Db.InternalMessageReadStates);
        Assert.Empty(store.Db.NotificationUserStates);
    }

    [Fact]
    public void GroupStorageQueriesTranslateWithNpgsqlAndDefaultModeDoesNotReferenceNewTable()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var user = new User("Developer");
        var legacy = new InternalMailAreaAccessService(db, user);
        var mapped = MappedAccess(db, user);
        Assert.DoesNotContain("InternalConversationGroups", legacy.InboxConversations().ToQueryString());
        foreach (var sql in new[] { mapped.Conversations().ToQueryString(), mapped.Messages("technical").ToQueryString(),
                     mapped.VisibleGroupScopes().ToQueryString(), mapped.InboxConversations().ToQueryString(),
                     mapped.GroupChildren(Guid.NewGuid()).ToQueryString() })
        {
            Assert.Contains("InternalConversationGroups", sql);
            Assert.Contains("CompanyId", sql);
        }
    }
}
