using System.Text.Json;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Enums.InternalMailEnums;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace HRM.Application.Tests.Features.InternalMail.GroupStorage;

public sealed partial class InternalConversationGroupStoragePostgresTests(IsolatedGroupStoragePostgres postgres)
    : IClassFixture<IsolatedGroupStoragePostgres>, IAsyncLifetime
{
    private const string Root = "20000000-0000-0000-0000-000000000001";
    private const string Technical = "30000000-0000-0000-0000-000000000001";
    private const string Custom = "30000000-0000-0000-0000-000000000004";
    private const string Company = "10000000-0000-0000-0000-000000000001";
    private const string Employee = "00000000-0000-0000-0000-000000000001";
    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<T> ScalarAsync<T>(string sql)
    {
        await using var connection = await postgres.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return (T)Convert.ChangeType((await command.ExecuteScalarAsync())!, typeof(T));
    }

    private async Task AssertExpansionAbsentAsync()
    {
        Assert.True(await ScalarAsync<bool>("SELECT to_regclass('\"InternalMail\".\"InternalConversationGroups\"') IS NULL"));
        Assert.True(await ScalarAsync<bool>("SELECT to_regclass('\"InternalMail\".\"UX_GroupStorage_Conversations_Company_Id\"') IS NULL"));
    }

    [PostgresGroupStorageFact]
    public async Task ExpansionPreservesAllProtectedRowsAndIndependentUnreadQueries()
    {
        var before = await ProtectedRowsAsync();
        await using var connection = await postgres.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        var unreadBefore = await db.QueryInternalConversationNotificationUnreadCounts(Guid.Parse(Company), Guid.Parse(Employee)).ToArrayAsync();
        Assert.Equal(1, Assert.Single(unreadBefore).UnreadCount);
        await postgres.ApplyAsync();
        Assert.Equal(before, await ProtectedRowsAsync());
        var unreadAfter = await db.QueryInternalConversationNotificationUnreadCounts(Guid.Parse(Company), Guid.Parse(Employee)).ToArrayAsync();
        Assert.Equal(JsonSerializer.Serialize(unreadBefore), JsonSerializer.Serialize(unreadAfter));
        var mappings = await db.InternalConversationGroups.AsNoTracking().ToArrayAsync();
        Assert.Equal(8, mappings.Length);
        Assert.Equal(2, mappings.Count(g => g.GroupType == InternalConversationGroupType.Custom));
        Assert.Equal("Thử mẫu đợt 2", mappings.Single(g => g.ConversationId == Guid.Parse(Custom)).DisplayName);
        Assert.All(mappings.Where(g => g.GroupType != InternalConversationGroupType.Custom), g => Assert.Null(g.DisplayName));
        await postgres.ExecuteAsync(await File.ReadAllTextAsync(postgres.ScriptPath("03-verify.readonly.sql")));
    }

    [PostgresGroupStorageFact]
    public async Task EfCanRunLegacyQueriesWithoutExpandedTable()
    {
        await using var connection = await postgres.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        Assert.DoesNotContain("InternalConversationGroups", db.InternalConversations.AsNoTracking().ToQueryString());
        Assert.Equal(8, await db.InternalConversations.CountAsync());
        Assert.NotEmpty(await db.QueryInternalConversationNotificationUnreadCounts(Guid.Parse(Company), Guid.Parse(Employee)).ToArrayAsync());
        await AssertExpansionAbsentAsync();
    }

    [PostgresGroupStorageFact]
    public async Task ApprovalGuardCannotAccidentallyCreateSchema()
    {
        var before = await ProtectedRowsAsync();
        var error = await Assert.ThrowsAsync<PostgresException>(() => postgres.ApplyAsync(approved: false));
        Assert.Equal("P0001", error.SqlState);
        await AssertExpansionAbsentAsync();
        Assert.Equal(before, await ProtectedRowsAsync());
    }

    [PostgresGroupStorageFact]
    public async Task RepeatedExpansionIsRejectedWithoutOverwritingMappings()
    {
        await postgres.ApplyAsync();
        await postgres.ExecuteAsync("UPDATE \"InternalMail\".\"InternalConversationGroups\" SET \"DisplayName\"='Tên giữ nguyên' WHERE \"ConversationId\"='30000000-0000-0000-0000-000000000004'");
        var before = await ProtectedRowsAsync();
        await Assert.ThrowsAsync<PostgresException>(() => postgres.ApplyAsync());
        Assert.Equal("Tên giữ nguyên", await ScalarAsync<string>("SELECT \"DisplayName\" FROM \"InternalMail\".\"InternalConversationGroups\" WHERE \"ConversationId\"='30000000-0000-0000-0000-000000000004'"));
        Assert.Equal(before, await ProtectedRowsAsync());
    }

    [PostgresGroupStorageFact]
    public async Task EfLegacyInsertCreatesExactlyOneMappingAndRollsBackTogether()
    {
        await postgres.ApplyAsync();
        var id = Guid.NewGuid();
        await using var connection = await postgres.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            db.InternalConversations.Add(new InternalConversation
            {
                InternalConversationId = id, CompanyId = Guid.Parse(Company), CreatedBy = Guid.Parse(Employee),
                Subject = "Nhóm tạo qua EF", RelatedType = InternalMailRelatedType.ConversationCustom, RelatedId = Guid.Parse(Root),
                CreatedAt = new DateTime(2026,10,31), LastMessageAt = new DateTime(2026,10,1), IsActive = true
            });
            await db.SaveChangesAsync();
            Assert.Equal(1, await db.InternalConversationGroups.CountAsync(g => g.ConversationId == id));
            await transaction.RollbackAsync();
        }
        Assert.False(await db.InternalConversations.AsNoTracking().AnyAsync(c => c.InternalConversationId == id));
        Assert.False(await db.InternalConversationGroups.AsNoTracking().AnyAsync(g => g.ConversationId == id));
    }

    [PostgresGroupStorageFact]
    public async Task LegacySubjectChangeSynchronizesCustomNameWithoutBumpingActivity()
    {
        await postgres.ApplyAsync();
        var activity = await ScalarAsync<string>("SELECT md5(string_agg(\"CreatedAt\"::text||\"LastMessageAt\"::text,'' ORDER BY \"InternalConversationId\")) FROM \"InternalMail\".\"InternalConversations\"");
        await postgres.ExecuteAsync("UPDATE \"InternalMail\".\"InternalConversations\" SET \"Subject\"='Tên hồ sơ mới' WHERE \"InternalConversationId\"='20000000-0000-0000-0000-000000000001'; UPDATE \"InternalMail\".\"InternalConversations\" SET \"Subject\"='Tên nhóm mới' WHERE \"InternalConversationId\"='30000000-0000-0000-0000-000000000004'");
        Assert.Equal("Tên nhóm mới", await ScalarAsync<string>("SELECT \"DisplayName\" FROM \"InternalMail\".\"InternalConversationGroups\" WHERE \"ConversationId\"='30000000-0000-0000-0000-000000000004'"));
        Assert.True(await ScalarAsync<bool>("SELECT \"DisplayName\" IS NULL FROM \"InternalMail\".\"InternalConversationGroups\" WHERE \"ConversationId\"='20000000-0000-0000-0000-000000000001'"));
        Assert.Equal(activity, await ScalarAsync<string>("SELECT md5(string_agg(\"CreatedAt\"::text||\"LastMessageAt\"::text,'' ORDER BY \"InternalConversationId\")) FROM \"InternalMail\".\"InternalConversations\""));
    }

    private async Task<string> ProtectedRowsAsync() => await ScalarAsync<string>("""
        SELECT jsonb_build_object(
          'conversations',(SELECT jsonb_agg(to_jsonb(c) ORDER BY "InternalConversationId") FROM "InternalMail"."InternalConversations" c),
          'participants',(SELECT jsonb_agg(to_jsonb(p) ORDER BY "InternalConversationId","EmployeeId") FROM "InternalMail"."InternalConversationParticipants" p),
          'messages',(SELECT jsonb_agg(to_jsonb(m) ORDER BY "InternalMessageId") FROM "InternalMail"."InternalMessages" m),
          'read_states',(SELECT jsonb_agg(to_jsonb(s) ORDER BY "InternalMessageId","EmployeeId") FROM "InternalMail"."InternalMessageReadStates" s),
          'references',(SELECT jsonb_agg(to_jsonb(r) ORDER BY "InternalMessageReferenceId") FROM "InternalMail"."InternalMessageReferences" r),
          'attachments',(SELECT jsonb_agg(to_jsonb(a) ORDER BY "InternalMessageId","AttachmentId") FROM "InternalMail"."InternalMessageAttachments" a),
          'notifications',(SELECT jsonb_agg(to_jsonb(n) ORDER BY id) FROM notification.notifications n),
          'user_states',(SELECT jsonb_agg(to_jsonb(s) ORDER BY notification_id,user_id) FROM notification.notification_user_states s),
          'recipients',(SELECT jsonb_agg(to_jsonb(r) ORDER BY id) FROM notification.notification_recipients r),
          'outbox',(SELECT jsonb_agg(to_jsonb(o) ORDER BY id) FROM notification.outbox_messages o)
        )::text
        """);
}
