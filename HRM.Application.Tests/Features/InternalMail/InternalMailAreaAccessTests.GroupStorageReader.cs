using HRM.Application.Features.InternalMail.Services;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Enums.InternalMailEnums;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace HRM.Application.Tests.Features.InternalMail;

public sealed partial class InternalMailAreaAccessTests
{
    [Theory]
    [InlineData("President", false, false)]
    [InlineData("Developer", false, true)]
    [InlineData("LabUser", false, false)]
    [InlineData("SaleUser", false, false)]
    [InlineData("PLPUUser", false, false)]
    [InlineData("LabUser", true, true)]
    [InlineData("SaleUser", true, true)]
    [InlineData("PLPUUser", true, true)]
    public async Task MappingReaderReusesExistingAccessWithoutGrantingMembership(string role, bool member, bool expected)
    {
        var user = new User(role);
        var store = CreateStore(user, InternalMailRelatedType.ConversationCustom, member);
        store.Root.RelatedType = InternalMailRelatedType.SampleRequest;
        var memory = (MemoryStore)(object)store.Db;
        var rootMapping = new InternalConversationGroup
        {
            ConversationId = store.Root.InternalConversationId,
            RootConversationId = store.Root.InternalConversationId,
            CompanyId = user.CompanyId!.Value,
            GroupType = InternalConversationGroupType.General,
            IsActive = true
        };
        var mapping = new InternalConversationGroup
        {
            ConversationId = store.Child.InternalConversationId,
            RootConversationId = store.Root.InternalConversationId,
            CompanyId = user.CompanyId.Value,
            GroupType = InternalConversationGroupType.Custom,
            IsActive = true,
            DisplayName = "Tên nhóm riêng"
        };
        memory.Values[nameof(store.Db.InternalConversationGroups)] = new MemorySet<InternalConversationGroup>([rootMapping, mapping]);
        var access = new InternalMailAreaAccessService(store.Db, user);
        var before = store.Child.Participants.Count;
        var rows = await new InternalConversationGroupReader(store.Db, user, access).ReadAsync([mapping.ConversationId], default);
        Assert.Equal(expected, rows.Count == 1);
        Assert.Equal(before, store.Child.Participants.Count);
        if (expected) Assert.Equal("Tên nhóm riêng", Assert.Single(rows).DisplayName);
        if (!member) Assert.Empty(store.Child.Participants);
    }

    [Theory]
    [InlineData("mapping-company")]
    [InlineData("root-company")]
    [InlineData("conversation-company")]
    [InlineData("mapping-inactive")]
    [InlineData("root-inactive")]
    [InlineData("parent-inactive")]
    [InlineData("parent-not-general")]
    [InlineData("root-not-self")]
    [InlineData("invalid-kind")]
    [InlineData("missing-mapping")]
    public async Task MappingReaderFailsClosedForInvalidMappings(string failure)
    {
        var user = new User("Developer");
        var store = CreateStore(user, InternalMailRelatedType.ConversationCustom, false);
        store.Root.RelatedType = InternalMailRelatedType.SampleRequest;
        var rootMapping = new InternalConversationGroup
        {
            ConversationId = store.Root.InternalConversationId,
            RootConversationId = store.Root.InternalConversationId,
            CompanyId = user.CompanyId!.Value,
            GroupType = InternalConversationGroupType.General,
            IsActive = true
        };
        var mapping = new InternalConversationGroup
        {
            ConversationId = store.Child.InternalConversationId,
            RootConversationId = store.Root.InternalConversationId,
            CompanyId = user.CompanyId.Value,
            GroupType = InternalConversationGroupType.Custom,
            IsActive = true
        };
        switch (failure)
        {
            case "mapping-company": mapping.CompanyId = Guid.NewGuid(); break;
            case "root-company": rootMapping.CompanyId = Guid.NewGuid(); break;
            case "conversation-company": store.Child.CompanyId = Guid.NewGuid(); break;
            case "mapping-inactive": mapping.IsActive = false; break;
            case "root-inactive": rootMapping.IsActive = false; break;
            case "parent-inactive": store.Root.IsActive = false; break;
            case "parent-not-general": rootMapping.GroupType = InternalConversationGroupType.Technical; break;
            case "root-not-self": rootMapping.RootConversationId = Guid.NewGuid(); break;
            case "invalid-kind": mapping.GroupType = (InternalConversationGroupType)99; break;
        }
        ((MemoryStore)(object)store.Db).Values[nameof(store.Db.InternalConversationGroups)] =
            new MemorySet<InternalConversationGroup>(failure == "missing-mapping" ? [rootMapping] : [rootMapping, mapping]);
        var access = new InternalMailAreaAccessService(store.Db, user);
        Assert.Empty(await new InternalConversationGroupReader(store.Db, user, access).ReadAsync([mapping.ConversationId], default));
    }

    [Fact]
    public async Task MappingReaderDoesNotProbeSchemaForAnonymousOrEmptyRequestsAndBoundsBatches()
    {
        var user = new User("Developer") { IsAuthenticated = false };
        var store = CreateStore(user, InternalMailRelatedType.Internal, false);
        ((MemoryStore)(object)store.Db).Values[nameof(store.Db.InternalConversationGroups)] = null!;
        var reader = new InternalConversationGroupReader(store.Db, user, new InternalMailAreaAccessService(store.Db, user));
        Assert.Empty(await reader.ReadAsync([store.Child.InternalConversationId], default));
        user.IsAuthenticated = true;
        Assert.Empty(await reader.ReadAsync([], default));
        Assert.Empty(await reader.ReadAsync([Guid.Empty], default));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => reader.ReadAsync(Enumerable.Range(0, 201).Select(_ => Guid.NewGuid()).ToArray(), default));
    }

    [Fact]
    public void GroupEfModelMatchesManualSchemaAndDoesNotChangeLegacyReadQueries()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var entity = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(InternalConversationGroup))!;
        Assert.Equal("InternalMail", entity.GetSchema());
        Assert.Equal("InternalConversationGroups", entity.GetTableName());
        Assert.True(entity.IsTableExcludedFromMigrations());
        Assert.Equal(6, entity.GetProperties().Count());
        Assert.DoesNotContain(entity.GetProperties(), p => p.IsShadowProperty());
        Assert.Equal(typeof(short), entity.FindProperty(nameof(InternalConversationGroup.GroupType))!.GetProviderClrType());
        Assert.Equal(100, entity.FindProperty(nameof(InternalConversationGroup.DisplayName))!.GetMaxLength());
        Assert.Equal(2, entity.GetForeignKeys().Count());
        Assert.All(entity.GetForeignKeys(), fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
        Assert.Contains(entity.GetIndexes(), i => i.IsUnique && i.GetFilter() == "\"IsActive\" AND \"GroupType\" IN (1,2)");
        Assert.DoesNotContain("CREATE TABLE \"InternalMail\".\"InternalConversationGroups\"", db.Database.GenerateCreateScript());
        var user = new User("Developer");
        var access = new InternalMailAreaAccessService(db, user);
        Assert.DoesNotContain("InternalConversationGroups", access.Conversations().ToQueryString());
        Assert.DoesNotContain("InternalConversationGroups", access.Messages().ToQueryString());
        var sql = new InternalConversationGroupReader(db, user, access).Query(user.CompanyId!.Value, [Guid.NewGuid()]).ToQueryString();
        Assert.Contains("InternalConversationGroups", sql);
        Assert.Contains("CompanyId", sql);
    }
}
