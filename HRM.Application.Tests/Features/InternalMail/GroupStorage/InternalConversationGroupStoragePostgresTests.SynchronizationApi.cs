using ClosedXML.Excel;
using HRM.Application.Features.InternalMail.GroupStorage;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Enums.InternalMailEnums;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using HRM.Infrastructure.Documents.Excels;
using HRM.Infrastructure.Services.InternalMail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HRM.Application.Tests.Features.InternalMail.GroupStorage;

public sealed partial class InternalConversationGroupStoragePostgresTests
{
    // GROUP-STORAGE-TRANSITION: synchronization-api — regression for schema-only deployment, with NO installed triggers.
    private async Task SchemaOnlyAsync()
    {
        await postgres.ApplyAsync();
        await postgres.ExecuteAsync("""
            DROP TRIGGER "TR_SyncLegacyConversationGroup" ON "InternalMail"."InternalConversations";
            DROP TRIGGER "TR_ValidateConversationGroup" ON "InternalMail"."InternalConversationGroups";
            DROP FUNCTION "InternalMail"."SyncLegacyConversationGroup"();
            DROP FUNCTION "InternalMail"."ValidateConversationGroup"();
            TRUNCATE "InternalMail"."InternalConversationGroups";
            """);
        await BusinessStubsAsync();
    }

    private Task BusinessStubsAsync() => postgres.ExecuteAsync("""
        ALTER TABLE hr."Employees" ADD COLUMN IF NOT EXISTS "IsActive" boolean NOT NULL DEFAULT true;
        CREATE TABLE IF NOT EXISTS public."AspNetRoles" ("Id" uuid PRIMARY KEY, "Name" text, "NormalizedName" text, "ConcurrencyStamp" text);
        CREATE TABLE IF NOT EXISTS public."AspNetUsers" ("Id" uuid PRIMARY KEY, "EmployeeId" uuid);
        CREATE TABLE IF NOT EXISTS public."AspNetUserRoles" ("UserId" uuid, "RoleId" uuid, "IsActive" boolean);
        TRUNCATE public."AspNetUserRoles", public."AspNetUsers";
        CREATE SCHEMA IF NOT EXISTS "SampleRequests";
        CREATE TABLE IF NOT EXISTS "SampleRequests"."SampleRequests" ("SampleRequestId" uuid, "CompanyId" uuid, "ProductId" uuid, "ExternalId" text);
        CREATE TABLE IF NOT EXISTS "SampleRequests"."Products" ("ProductId" uuid, "CompanyId" uuid, "ColourCode" text);
        """);

    private static GroupStorageSynchronization Synchronizer(ApplicationDbContext db, IGroupStorageSyncExcelRenderer? excel = null,
        bool useMappedGroups = false) =>
        new(db, excel ?? new GroupStorageSyncExcelRenderer(), NullLogger<GroupStorageSynchronization>.Instance,
            new PresidentMembershipBackfill(db, new HRM.Application.Features.InternalMail.Services.InternalMailAreaAccessService(db, new CutoverUser(),
                Microsoft.Extensions.Options.Options.Create(new HRM.Application.Features.InternalMail.Services.InternalMailGroupStorageOptions { Enabled = useMappedGroups }))));

    [PostgresGroupStorageFact]
    public async Task SynchronizationApiIsIdempotentExportsEveryRunAndPreservesProtectedData()
    {
        await SchemaOnlyAsync();
        var before = await ProtectedRowsAsync();
        await using var connection = await postgres.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        var first = await Synchronizer(db).SynchronizeAsync(Guid.Parse(Company), default);
        var second = await Synchronizer(db).SynchronizeAsync(Guid.Parse(Company), default);
        Assert.Equal("Completed", first.Outcome);
        Assert.Equal("Completed", second.Outcome);
        var artifactDirectory = Path.Combine(postgres.RepositoryRoot, "artifacts", "group-storage-api-results");
        Directory.CreateDirectory(artifactDirectory);
        await File.WriteAllBytesAsync(Path.Combine(artifactDirectory, "sync-demo.xlsx"), first.Content);
        Assert.NotEqual(first.RunId, second.RunId);
        Assert.NotEqual(first.FileName, second.FileName);
        Assert.Equal(before, await ProtectedRowsAsync());
        Assert.Equal(7, await db.InternalConversationGroups.CountAsync());
        Assert.All(await db.InternalConversationGroups.ToArrayAsync(), g => Assert.Equal(Guid.Parse(Company), g.CompanyId));
        using var initial = new XLWorkbook(new MemoryStream(first.Content));
        using var repeat = new XLWorkbook(new MemoryStream(second.Content));
        var firstRows = initial.Worksheet("Chi tiết nhóm").RowsUsed().Where(r => r.RowNumber() >= 7).ToArray();
        var repeatedRows = repeat.Worksheet("Chi tiết nhóm").RowsUsed().Where(r => r.RowNumber() >= 7).ToArray();
        Assert.Equal(7, firstRows.Length);
        Assert.All(firstRows, r => Assert.Equal("Mới đồng bộ", r.Cell(5).GetString()));
        Assert.All(repeatedRows, r => Assert.Equal("Đã có trước", r.Cell(5).GetString()));
        Assert.Equal(2, firstRows.Sum(r => r.Cell(6).GetValue<int>()));
        Assert.Equal(1, firstRows.Sum(r => r.Cell(7).GetValue<int>()));
        Assert.Equal(1, Assert.Single(await db.QueryInternalConversationNotificationUnreadCounts(Guid.Parse(Company), Guid.Parse(Employee)).ToArrayAsync()).UnreadCount);
    }

    [PostgresGroupStorageFact]
    public async Task SynchronizationApiMissingSchemaStillExportsWithoutCreatingAnything()
    {
        await BusinessStubsAsync();
        var before = await ProtectedRowsAsync();
        await using var connection = await postgres.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        var file = await Synchronizer(db).SynchronizeAsync(Guid.Parse(Company), default);
        Assert.Equal("Blocked", file.Outcome);
        using var workbook = new XLWorkbook(new MemoryStream(file.Content));
        Assert.Contains("SchemaRequired", workbook.Worksheet(1).Cell(4, 1).GetString());
        Assert.Equal(before, await ProtectedRowsAsync());
        await AssertExpansionAbsentAsync();
    }

    [PostgresGroupStorageFact]
    public async Task SynchronizationApiBlocksConflictingMappingInsteadOfOverwritingIt()
    {
        await SchemaOnlyAsync();
        await postgres.ExecuteAsync("""
            INSERT INTO "InternalMail"."InternalConversationGroups" VALUES
             ('20000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000001','20000000-0000-0000-0000-000000000001',0,NULL,true),
             ('30000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000001','20000000-0000-0000-0000-000000000001',2,NULL,true);
            """);
        var before = await ProtectedRowsAsync();
        await using var connection = await postgres.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        var file = await Synchronizer(db).SynchronizeAsync(Guid.Parse(Company), default);
        Assert.Equal("Blocked", file.Outcome);
        Assert.Equal(2, await db.InternalConversationGroups.CountAsync());
        Assert.Equal(before, await ProtectedRowsAsync());
        using var workbook = new XLWorkbook(new MemoryStream(file.Content));
        Assert.Contains("ExistingMappingConflict", workbook.Worksheet(1).Cell(4, 1).GetString());
    }

    [PostgresGroupStorageFact]
    public async Task SynchronizationApiConcurrentRunsDoNotCreateDuplicates()
    {
        await SchemaOnlyAsync();
        async Task<GroupStorageSyncFile> Run()
        {
            await using var connection = await postgres.OpenAsync();
            await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
            return await Synchronizer(db).SynchronizeAsync(Guid.Parse(Company), default);
        }
        var files = await Task.WhenAll(Run(), Run());
        Assert.All(files, file => Assert.Equal("Completed", file.Outcome));
        Assert.NotEqual(files[0].RunId, files[1].RunId);
        Assert.Equal(7L, await ScalarAsync<long>("SELECT count(*) FROM \"InternalMail\".\"InternalConversationGroups\""));
    }

    [PostgresGroupStorageFact]
    public async Task SynchronizationApiExportFailureRollsBackAllMappings()
    {
        await SchemaOnlyAsync();
        var before = await ProtectedRowsAsync();
        await using var connection = await postgres.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Synchronizer(db, new BrokenExcel()).SynchronizeAsync(Guid.Parse(Company), default));
        Assert.Empty(await db.InternalConversationGroups.ToArrayAsync());
        Assert.Equal(before, await ProtectedRowsAsync());
    }

    private sealed class BrokenExcel : IGroupStorageSyncExcelRenderer
    {
        public byte[] Render(GroupStorageSyncReport report) => throw new InvalidOperationException("Synthetic export failure");
    }

    [PostgresGroupStorageFact]
    public async Task AtomicWriterCreatesAndUpdatesGroupsWithoutTriggersOrSynchronizationApi()
    {
        await SchemaOnlyAsync();
        await using var connection = await postgres.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        var child = NewChild();
        db.InternalConversations.Add(child);
        await db.SaveChangesAsync();
        Assert.Equal(2, await db.InternalConversationGroups.CountAsync()); // root + this child only
        Assert.Equal(child.Subject, (await db.InternalConversationGroups.AsNoTracking().SingleAsync(g => g.ConversationId == child.InternalConversationId)).DisplayName);
        var activity = child.LastMessageAt;
        child.Subject = "Tên nhóm mới";
        child.IsActive = false;
        await db.SaveChangesAsync();
        var mapping = await db.InternalConversationGroups.AsNoTracking().SingleAsync(g => g.ConversationId == child.InternalConversationId);
        Assert.Equal("Tên nhóm mới", mapping.DisplayName);
        Assert.False(mapping.IsActive);
        Assert.Equal(activity, child.LastMessageAt);
        child.Subject = "Tên lưu bằng SaveChanges đồng bộ";
        db.SaveChanges();
        Assert.Equal(child.Subject, (await db.InternalConversationGroups.AsNoTracking().SingleAsync(g => g.ConversationId == child.InternalConversationId)).DisplayName);
    }

    [PostgresGroupStorageFact]
    public async Task AtomicWriterUsesSavepointSoCallerCannotCommitHalfAGroup()
    {
        await SchemaOnlyAsync();
        await using var connection = await postgres.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        await using var outer = await db.Database.BeginTransactionAsync();
        var child = NewChild();
        child.Subject = new string('x', 101);
        db.InternalConversations.Add(child);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        await outer.CommitAsync();
        Assert.False(await db.InternalConversations.AsNoTracking().AnyAsync(c => c.InternalConversationId == child.InternalConversationId));
        Assert.Empty(await db.InternalConversationGroups.ToArrayAsync());
    }

    [PostgresGroupStorageFact]
    public async Task MessageActivityOnlySaveDoesNotBackfillOrBumpOtherGroups()
    {
        await SchemaOnlyAsync();
        await using var connection = await postgres.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        var root = await db.InternalConversations.SingleAsync(c => c.InternalConversationId == Guid.Parse(Root));
        root.LastMessageAt = root.LastMessageAt.AddMinutes(1);
        await db.SaveChangesAsync();
        Assert.Empty(await db.InternalConversationGroups.ToArrayAsync());
    }

    [PostgresGroupStorageFact]
    public async Task AtomicWriterRollsBackConversationAndRootMappingWhenGroupInsertFails()
    {
        await SchemaOnlyAsync();
        await postgres.ExecuteAsync("ALTER TABLE \"InternalMail\".\"InternalConversationGroups\" ADD CONSTRAINT fixture_block_custom CHECK (\"GroupType\" <> 3)");
        await using var connection = await postgres.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        var child = NewChild();
        db.InternalConversations.Add(child);
        var error = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.SaveChangesAsync());
        Assert.Equal("23514", error.SqlState);
        Assert.False(await db.InternalConversations.AsNoTracking().AnyAsync(c => c.InternalConversationId == child.InternalConversationId));
        Assert.Empty(await db.InternalConversationGroups.ToArrayAsync());
    }

    [PostgresGroupStorageFact]
    public async Task AtomicWriterAndSynchronizationApiCanRunConcurrently()
    {
        await SchemaOnlyAsync();
        var child = NewChild();
        async Task Write()
        {
            await using var connection = await postgres.OpenAsync();
            await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
            await using var transaction = await db.Database.BeginTransactionAsync();
            await db.LockInternalConversationAsync(Guid.Parse(Company), Guid.Parse(Root), default);
            db.InternalConversations.Add(child);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        async Task Sync()
        {
            await using var connection = await postgres.OpenAsync();
            await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
            Assert.Equal("Completed", (await Synchronizer(db).SynchronizeAsync(Guid.Parse(Company), default)).Outcome);
        }
        await Task.WhenAll(Write(), Sync());
        Assert.Equal(8L, await ScalarAsync<long>("SELECT count(*) FROM \"InternalMail\".\"InternalConversationGroups\""));
    }

    private static InternalConversation NewChild() => new()
    {
        InternalConversationId = Guid.CreateVersion7(), CompanyId = Guid.Parse(Company), CreatedBy = Guid.Parse(Employee),
        Subject = "Nhóm mới không trigger", RelatedType = InternalMailRelatedType.ConversationCustom, RelatedId = Guid.Parse(Root),
        CreatedAt = new DateTime(2026, 11, 1), LastMessageAt = new DateTime(2026, 10, 1), IsActive = true
    };
}
