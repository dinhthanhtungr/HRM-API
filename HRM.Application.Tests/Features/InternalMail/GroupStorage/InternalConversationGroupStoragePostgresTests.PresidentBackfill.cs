using ClosedXML.Excel;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HRM.Application.Tests.Features.InternalMail.GroupStorage;

public sealed partial class InternalConversationGroupStoragePostgresTests
{
    private const string MissingPresident = "00000000-0000-0000-0000-000000000002";
    private const string Pricing = "30000000-0000-0000-0000-000000000003";

    private async Task PresidentBusinessSchemaAsync()
    {
        await SchemaOnlyAsync();
        await postgres.ExecuteAsync("""
            ALTER TABLE "SampleRequests"."SampleRequests" ADD COLUMN IF NOT EXISTS "IsActive" boolean DEFAULT true;
            ALTER TABLE "SampleRequests"."Products" ADD COLUMN IF NOT EXISTS "CategoryId" uuid;
            TRUNCATE "SampleRequests"."SampleRequests", "SampleRequests"."Products";
            CREATE SCHEMA IF NOT EXISTS "Material";
            CREATE TABLE IF NOT EXISTS "Material"."Categories" ("CategoryId" uuid PRIMARY KEY, "CompanyId" uuid, "ExternalId" text);
            CREATE SCHEMA IF NOT EXISTS "Customer";
            CREATE TABLE IF NOT EXISTS "Customer"."Quotations" ("QuotationId" uuid, "CompanyId" uuid);
            CREATE TABLE IF NOT EXISTS "Customer"."QuotationLines" ("QuotationLineId" uuid, "QuotationId" uuid, "ProductId" uuid, "IsActive" boolean);
            CREATE SCHEMA IF NOT EXISTS company;
            CREATE TABLE IF NOT EXISTS company."Groups" ("GroupId" uuid PRIMARY KEY, "CompanyId" uuid, "GroupType" text);
            CREATE TABLE IF NOT EXISTS company."MemberInGroup" ("MemberId" uuid, "GroupId" uuid, "Profile" uuid, "IsAdmin" boolean, "IsActive" boolean);
            TRUNCATE company."MemberInGroup";
            INSERT INTO "SampleRequests"."SampleRequests" ("SampleRequestId","CompanyId","ProductId","ExternalId","IsActive") VALUES
              ('90000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000001','91000000-0000-0000-0000-000000000001','TP_TEST',true);
            INSERT INTO "SampleRequests"."Products" ("ProductId","CompanyId","ColourCode") VALUES
              ('91000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000001','TEST_COLOR');
            INSERT INTO public."AspNetRoles" ("Id","Name","NormalizedName") VALUES
              ('b1000000-0000-0000-0000-000000000001','President','PRESIDENT'),
              ('b1000000-0000-0000-0000-000000000002','Developer','DEVELOPER') ON CONFLICT DO NOTHING;
            INSERT INTO public."AspNetUsers" VALUES
              ('a1000000-0000-0000-0000-000000000001','00000000-0000-0000-0000-000000000001'),
              ('a1000000-0000-0000-0000-000000000002','00000000-0000-0000-0000-000000000002'),
              ('a1000000-0000-0000-0000-000000000003','00000000-0000-0000-0000-000000000003');
            INSERT INTO public."AspNetUserRoles" SELECT "Id",'b1000000-0000-0000-0000-000000000001',true FROM public."AspNetUsers";
            """);
    }

    private async Task<string> PresidentProtectedRowsAsync()
    {
        using var document = System.Text.Json.JsonDocument.Parse(await ProtectedRowsAsync());
        return System.Text.Json.JsonSerializer.Serialize(document.RootElement.EnumerateObject()
            .Where(p => p.Name != "participants").ToDictionary(p => p.Name, p => p.Value.Clone()));
    }

    [PostgresGroupStorageTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CombinedSynchronizationAddsOnlyMissingDefaultPresidentsAndExportsRepeatableAudit(bool useMappedGroups)
    {
        await PresidentBusinessSchemaAsync();
        var before = await PresidentProtectedRowsAsync();
        var membersBefore = await ScalarAsync<string>("SELECT jsonb_agg(to_jsonb(p) ORDER BY \"InternalConversationId\")::text FROM \"InternalMail\".\"InternalConversationParticipants\" p WHERE \"EmployeeId\"='00000000-0000-0000-0000-000000000001'");
        await using var connection = await postgres.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        var file = await Synchronizer(db, useMappedGroups: useMappedGroups).SynchronizeAsync(Guid.Parse(Company), default);
        Assert.Equal("Completed", file.Outcome);
        var added = await db.InternalConversationParticipants.AsNoTracking().Where(p => p.EmployeeId == Guid.Parse(MissingPresident) && p.IsActive).ToArrayAsync();
        Assert.Equal(new[] { Guid.Parse(Technical), Guid.Parse(Pricing) }.Order(), added.Select(p => p.InternalConversationId).Order());
        Assert.All(added, p => { Assert.Equal(HRM.Domain.Enums.InternalMailEnums.InternalConversationParticipantRole.Member, p.Role); Assert.Null(p.LastReadAt); Assert.False(p.IsMuted); Assert.False(p.IsArchived); });
        Assert.Equal(before, await PresidentProtectedRowsAsync());
        Assert.Equal(membersBefore, await ScalarAsync<string>("SELECT jsonb_agg(to_jsonb(p) ORDER BY \"InternalConversationId\")::text FROM \"InternalMail\".\"InternalConversationParticipants\" p WHERE \"EmployeeId\"='00000000-0000-0000-0000-000000000001'"));
        using var workbook = new XLWorkbook(new MemoryStream(file.Content));
        Assert.Equal(3, workbook.Worksheets.Count);
        var sheet = workbook.Worksheet("Bù President");
        Assert.Equal(2, sheet.RowsUsed().Count(r => r.RowNumber() >= 7 && r.Cell(5).GetString() == "Đã bổ sung"));
        Assert.DoesNotContain(sheet.RowsUsed().Skip(6), r => r.Cell(4).GetString() == "00000000-0000-0000-0000-000000000003");
        var repeat = await Synchronizer(db, useMappedGroups: useMappedGroups).SynchronizeAsync(Guid.Parse(Company), default);
        Assert.Equal("Completed", repeat.Outcome);
        Assert.NotEqual(file.RunId, repeat.RunId);
        using var repeated = new XLWorkbook(new MemoryStream(repeat.Content));
        Assert.DoesNotContain(repeated.Worksheet("Bù President").RowsUsed().Skip(6), r => r.Cell(5).GetString() == "Đã bổ sung");
        Assert.Equal(before, await PresidentProtectedRowsAsync());
        var directory = Path.Combine(postgres.RepositoryRoot, "artifacts", "president-sync-results");
        Directory.CreateDirectory(directory);
        await File.WriteAllBytesAsync(Path.Combine(directory, "combined-sync-synthetic.xlsx"), file.Content);
    }

    [PostgresGroupStorageFact]
    public async Task PresidentBackfillNeverReactivatesRemovedMemberOrGrantsCustom()
    {
        await PresidentBusinessSchemaAsync();
        await postgres.ExecuteAsync("""
            INSERT INTO "InternalMail"."InternalConversationParticipants" ("InternalConversationId","EmployeeId","IsActive","IsMuted","IsArchived")
            VALUES ('30000000-0000-0000-0000-000000000001','00000000-0000-0000-0000-000000000002',false,true,true);
            """);
        await using var connection = await postgres.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        var file = await Synchronizer(db).SynchronizeAsync(Guid.Parse(Company), default);
        Assert.Equal("Completed", file.Outcome);
        var removed = await db.InternalConversationParticipants.AsNoTracking().SingleAsync(p => p.InternalConversationId == Guid.Parse(Technical) && p.EmployeeId == Guid.Parse(MissingPresident));
        Assert.False(removed.IsActive); Assert.True(removed.IsMuted); Assert.True(removed.IsArchived);
        Assert.False((await db.InternalConversationParticipants.AsNoTracking().SingleAsync(p => p.InternalConversationId == Guid.Parse(Custom) && p.EmployeeId == Guid.Parse(MissingPresident))).IsActive);
        using var workbook = new XLWorkbook(new MemoryStream(file.Content));
        Assert.Contains(workbook.Worksheet("Bù President").RowsUsed().Skip(6), r => r.Cell(5).GetString() == "Đã bị gỡ - giữ nguyên");
    }

    [PostgresGroupStorageFact]
    public async Task CombinedExportFailureRollsBackMappingsAndMembershipsTogether()
    {
        await PresidentBusinessSchemaAsync();
        var before = await ProtectedRowsAsync();
        await using var connection = await postgres.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Synchronizer(db, new BrokenExcel()).SynchronizeAsync(Guid.Parse(Company), default));
        Assert.Equal(before, await ProtectedRowsAsync());
        Assert.Empty(await db.InternalConversationGroups.AsNoTracking().ToArrayAsync());
    }

    [PostgresGroupStorageFact]
    public async Task ExplicitMappingOnlyRetainsPreviousNoMembershipWriteBehavior()
    {
        await PresidentBusinessSchemaAsync();
        var before = await ProtectedRowsAsync();
        await using var connection = await postgres.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        var file = await Synchronizer(db).SynchronizeAsync(Guid.Parse(Company), default, includePresidentMemberships: false);
        Assert.Equal("Completed", file.Outcome);
        Assert.Equal(before, await ProtectedRowsAsync());
        using var workbook = new XLWorkbook(new MemoryStream(file.Content));
        Assert.Equal(6, workbook.Worksheet("Bù President").LastRowUsed()!.RowNumber());
    }

    [PostgresGroupStorageFact]
    public async Task ConcurrentCombinedRunsSerializeMembershipInsertions()
    {
        await PresidentBusinessSchemaAsync();
        async Task<HRM.Application.Features.InternalMail.GroupStorage.GroupStorageSyncFile> Run()
        {
            await using var connection = await postgres.OpenAsync();
            await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
            return await Synchronizer(db).SynchronizeAsync(Guid.Parse(Company), default);
        }
        var files = await Task.WhenAll(Run(), Run());
        Assert.All(files, file => Assert.Equal("Completed", file.Outcome));
        var additions = files.Select(file => {
            using var workbook = new XLWorkbook(new MemoryStream(file.Content));
            return workbook.Worksheet("Bù President").RowsUsed().Count(r => r.RowNumber() >= 7 && r.Cell(5).GetString() == "Đã bổ sung");
        }).Order().ToArray();
        Assert.Equal(new[] { 0, 2 }, additions);
    }

    [PostgresGroupStorageTheory]
    [InlineData("inactive-employee")]
    [InlineData("inactive-role")]
    [InlineData("developer-too")]
    public async Task BackfillDoesNotGrantInactiveOrDeveloperPresidents(string boundary)
    {
        await PresidentBusinessSchemaAsync();
        var sql = boundary switch {
            "inactive-employee" => "UPDATE hr.\"Employees\" SET \"IsActive\"=false WHERE \"EmployeeID\"='00000000-0000-0000-0000-000000000002'",
            "inactive-role" => "UPDATE public.\"AspNetUserRoles\" SET \"IsActive\"=false WHERE \"UserId\"='a1000000-0000-0000-0000-000000000002'",
            _ => "INSERT INTO public.\"AspNetUserRoles\" VALUES ('a1000000-0000-0000-0000-000000000002','b1000000-0000-0000-0000-000000000002',true)"
        };
        await postgres.ExecuteAsync(sql);
        var before = await ProtectedRowsAsync();
        await using var connection = await postgres.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        Assert.Equal("Completed", (await Synchronizer(db).SynchronizeAsync(Guid.Parse(Company), default)).Outcome);
        Assert.Equal(before, await ProtectedRowsAsync());
    }

    [PostgresGroupStorageFact]
    public async Task BackfillKeepsRdColorEligibilityAndForwardSourceRestrictions()
    {
        await PresidentBusinessSchemaAsync();
        await postgres.ExecuteAsync("""
            INSERT INTO "Material"."Categories" ("CategoryId","CompanyId","ExternalId") VALUES
              ('c1000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000001','PIG') ON CONFLICT DO NOTHING;
            UPDATE "SampleRequests"."Products" SET "CategoryId"='c1000000-0000-0000-0000-000000000001';
            INSERT INTO company."Groups" VALUES ('d1000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000001','QAQC.RD') ON CONFLICT DO NOTHING;
            INSERT INTO company."MemberInGroup" VALUES ('d2000000-0000-0000-0000-000000000001','d1000000-0000-0000-0000-000000000001','00000000-0000-0000-0000-000000000002',true,true);
            """);
        var before = await ProtectedRowsAsync();
        await using var connection = await postgres.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        Assert.Equal("Completed", (await Synchronizer(db).SynchronizeAsync(Guid.Parse(Company), default)).Outcome);
        Assert.Equal(before, await ProtectedRowsAsync());
        await postgres.ExecuteAsync("""
            TRUNCATE company."MemberInGroup";
            INSERT INTO "InternalMail"."InternalMessages" ("InternalMessageId","InternalConversationId","SenderEmployeeId","Body")
            VALUES ('41000000-0000-0000-0000-000000000001','30000000-0000-0000-0000-000000000001','00000000-0000-0000-0000-000000000001','Synthetic forwarded text');
            INSERT INTO "InternalMail"."InternalMessageReferences" ("InternalMessageReferenceId","InternalMessageId","RelatedType","RelatedId")
            VALUES ('51000000-0000-0000-0000-000000000001','41000000-0000-0000-0000-000000000001',20,'20000000-0000-0000-0000-000000000001');
            """);
        Assert.Equal("Completed", (await Synchronizer(db).SynchronizeAsync(Guid.Parse(Company), default)).Outcome);
        var members = await db.InternalConversationParticipants.AsNoTracking().Where(p => p.EmployeeId == Guid.Parse(MissingPresident) && p.IsActive).ToArrayAsync();
        Assert.Equal(Guid.Parse(Pricing), Assert.Single(members).InternalConversationId);
    }
}

