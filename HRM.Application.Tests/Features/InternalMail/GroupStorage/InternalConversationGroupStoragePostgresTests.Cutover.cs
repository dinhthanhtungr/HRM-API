using System.Text.Json;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.InternalMail.Services;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace HRM.Application.Tests.Features.InternalMail.GroupStorage;

public sealed partial class InternalConversationGroupStoragePostgresTests
{
    [PostgresGroupStorageFact]
    public async Task CutoverQueriesExecuteAgainstPostgresAndPreserveAllRootAndUnreadLinks()
    {
        await using var connection = await postgres.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        var user = new CutoverUser();
        var legacy = new InternalMailAreaAccessService(db, user);
        var mapped = new InternalMailAreaAccessService(db, user, Options.Create(new InternalMailGroupStorageOptions { Enabled = true }));
        var company = Guid.Parse(Company);
        var before = await legacy.GroupScopes(company).OrderBy(g => g.ConversationId)
            .Select(g => new { g.ConversationId, g.RootConversationId, g.GroupType, g.Conversation.CreatedAt, g.Conversation.LastMessageAt }).ToArrayAsync();
        var protectedBefore = await ProtectedRowsAsync();
        var unreadBefore = await db.QueryInternalConversationNotificationUnreadCounts(company, Guid.Parse(Employee)).ToArrayAsync();
        await postgres.ApplyAsync();
        var after = await mapped.GroupScopes(company).OrderBy(g => g.ConversationId)
            .Select(g => new { g.ConversationId, g.RootConversationId, g.GroupType, g.Conversation.CreatedAt, g.Conversation.LastMessageAt }).ToArrayAsync();
        Assert.NotEmpty(before);
        Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(after));
        Assert.Equal(protectedBefore, await ProtectedRowsAsync());
        Assert.Equal(JsonSerializer.Serialize(unreadBefore), JsonSerializer.Serialize(
            await db.QueryInternalConversationNotificationUnreadCounts(company, Guid.Parse(Employee)).ToArrayAsync()));
        var mappings = await mapped.GroupInfoAsync(after.Select(g => g.ConversationId).ToArray(), default);
        Assert.Equal(after.Length, mappings.Count);
        Assert.Equal("Thử mẫu đợt 2", mappings[Guid.Parse(Custom)].DisplayName);
    }

    [PostgresGroupStorageFact]
    public async Task LegacyConversationWriterCreatesMappingAtomicallyForEnabledReader()
    {
        await postgres.ApplyAsync();
        // Synthetic fixture only: exercise the bridge used by all existing application writers.
        await postgres.ExecuteAsync("""
            INSERT INTO "InternalMail"."InternalConversations"
              ("InternalConversationId", "CompanyId", "Subject", "RelatedType", "RelatedId", "CreatedBy")
            VALUES ('30000000-0000-0000-0000-000000000099', '10000000-0000-0000-0000-000000000001', 'New empty group', 19,
                    '20000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000001');
            """);
        await using var connection = await postgres.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        var mapped = new InternalMailAreaAccessService(db, new CutoverUser(), Options.Create(new InternalMailGroupStorageOptions { Enabled = true }));
        var id = Guid.Parse("30000000-0000-0000-0000-000000000099");
        Assert.Equal(Guid.Parse(Root), await mapped.RootIdAsync(id, default));
        Assert.Equal("New empty group", (await mapped.GroupInfoAsync([id], default))[id].DisplayName);
        Assert.Equal(0, await db.InternalMessages.CountAsync(m => m.InternalConversationId == id));
        // Existing unread inbox is unaffected by an empty structural group.
        Assert.Equal(1, Assert.Single(await db.QueryInternalConversationNotificationUnreadCounts(Guid.Parse(Company), Guid.Parse(Employee)).ToArrayAsync()).UnreadCount);
    }

    private sealed class CutoverUser : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid UserId => Guid.Parse(Employee);
        public Guid? EmployeeId => Guid.Parse(Employee);
        public Guid? CompanyId => Guid.Parse(Company);
        public string? UserName => "synthetic";
        public string? Email => null;
        public IReadOnlyCollection<string> Roles => [];
        public bool IsInRole(string role) => false;
    }
}
