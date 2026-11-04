using HRM.Application.Abstractions.Security;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.InternalMail.DefaultMembership;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Enums.InternalMailEnums;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace HRM.Application.Tests.Features.InternalMail.GroupStorage;

public sealed partial class InternalConversationGroupStoragePostgresTests
{
    private static readonly Guid PolicyRole = Guid.Parse("b0000000-0000-0000-0000-000000000001");
    private static readonly Guid LocalCategory = Guid.Parse("c0000000-0000-0000-0000-000000000001");
    private static readonly Guid ForeignCategory = Guid.Parse("c0000000-0000-0000-0000-000000000002");

    private async Task PolicySchemaAsync()
    {
        // Synthetic cluster only; real deployment/migrations are left to the operator.
        await postgres.ExecuteAsync("""
            CREATE SCHEMA IF NOT EXISTS company;
            CREATE TABLE IF NOT EXISTS company."Companies" ("companyId" uuid PRIMARY KEY);
            CREATE TABLE IF NOT EXISTS company."Groups" ("GroupId" uuid PRIMARY KEY, "CompanyId" uuid, "GroupType" text);
            INSERT INTO company."Groups" VALUES
             ('d0000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000001','QAQC.RD'),
             ('d0000000-0000-0000-0000-000000000002','10000000-0000-0000-0000-000000000002','FOREIGN.LAB') ON CONFLICT DO NOTHING;
            INSERT INTO company."Companies" VALUES ('10000000-0000-0000-0000-000000000001'),('10000000-0000-0000-0000-000000000002') ON CONFLICT DO NOTHING;
            ALTER TABLE hr."Employees" ADD COLUMN IF NOT EXISTS "IsActive" boolean NOT NULL DEFAULT true;
            CREATE TABLE IF NOT EXISTS public."AspNetRoles" ("Id" uuid PRIMARY KEY, "Name" text, "NormalizedName" text, "ConcurrencyStamp" text);
            INSERT INTO public."AspNetRoles" ("Id","Name","NormalizedName") VALUES ('b0000000-0000-0000-0000-000000000001','LabUser','LABUSER') ON CONFLICT DO NOTHING;
            CREATE SCHEMA IF NOT EXISTS "Material";
            CREATE TABLE IF NOT EXISTS "Material"."Categories" ("CategoryId" uuid PRIMARY KEY, "CompanyId" uuid, "IsActive" boolean, "ExternalId" text, "Name" text);
            INSERT INTO "Material"."Categories" VALUES
             ('c0000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000001',true,'CMP','Local category'),
             ('c0000000-0000-0000-0000-000000000002','10000000-0000-0000-0000-000000000002',true,'PIG','Foreign category') ON CONFLICT DO NOTHING;
            """);
        using var modelDb = new MembershipSchemaContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var model = modelDb.GetService<IDesignTimeModel>().Model;
        var tableNames = new[] { "DefaultMembershipPolicies", "DefaultMembershipRevisions", "DefaultMembershipRules" };
        var operations = modelDb.GetService<IMigrationsModelDiffer>().GetDifferences(null, model.GetRelationalModel())
            .Where(op => op switch
            {
                CreateTableOperation t => tableNames.Contains(t.Name),
                CreateIndexOperation i => tableNames.Contains(i.Table),
                AddForeignKeyOperation fk => tableNames.Contains(fk.Table),
                _ => false
            }).ToArray();
        Assert.Equal(3, operations.OfType<CreateTableOperation>().Count());
        foreach (var command in modelDb.GetService<IMigrationsSqlGenerator>().Generate(operations, model))
            await postgres.ExecuteAsync(command.CommandText);
    }

    private sealed class MembershipSchemaContext(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.Entity<DefaultMembershipPolicy>().ToTable("DefaultMembershipPolicies", "InternalMail", t => t.ExcludeFromMigrations(false));
            builder.Entity<DefaultMembershipRevision>().ToTable("DefaultMembershipRevisions", "InternalMail", t => t.ExcludeFromMigrations(false));
            builder.Entity<DefaultMembershipRule>().ToTable("DefaultMembershipRules", "InternalMail", t => t.ExcludeFromMigrations(false));
        }
    }

    private sealed class PolicyAdmin : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid UserId => Guid.Parse(Employee);
        public Guid? EmployeeId => Guid.Parse(Employee);
        public Guid? CompanyId => Guid.Parse(Company);
        public string? UserName => "synthetic";
        public string? Email => null;
        public IReadOnlyCollection<string> Roles => [ApplicationRoles.Developer];
        public bool IsInRole(string role) => role == ApplicationRoles.Developer;
    }
    private sealed class PolicyClock : IDateTimeProvider { public DateTime Now => new(2026,11,1,12,0,0); }
    private static DefaultMembershipPolicyService PolicyService(ApplicationDbContext db)
    {
        var user = new PolicyAdmin();
        return new(db, user, new CurrentUserPermissionService(user), new PolicyClock());
    }
    private static PublishDefaultMembershipRequest PolicyRequest(int version) => new()
    {
        ExpectedVersion = version, Reason = "Synthetic policy publication",
        Rules = [new(DefaultMembershipRuleSource.CompanyRole, PolicyRole, null, DefaultMembershipCategoryScope.All, null)]
    };

    [PostgresGroupStorageFact]
    public async Task MembershipConfigurationPublishesHistoryAndRollbackWithoutChangingAnyChatData()
    {
        await PolicySchemaAsync();
        var before = await ProtectedRowsAsync();
        await using var connection = await postgres.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        var service = PolicyService(db);
        var initial = await service.PoliciesAsync(default);
        Assert.All(initial.Value!, p => Assert.False(p.Configured));
        Assert.Empty(await db.DefaultMembershipPolicies.ToArrayAsync());
        var v1 = await service.PublishAsync(InternalConversationGroupType.Technical, PolicyRequest(0), default);
        Assert.Equal(MembershipRequestStatus.Success, v1.Status);
        Assert.Equal(1, v1.Value!.Version);
        var stale = await service.PublishAsync(InternalConversationGroupType.Technical, PolicyRequest(0), default);
        Assert.Equal(MembershipRequestStatus.Conflict, stale.Status);
        var request2 = PolicyRequest(1); request2.Rules = [];
        Assert.Equal(MembershipRequestStatus.Success, (await service.PublishAsync(InternalConversationGroupType.Technical, request2, default)).Status);
        var restored = await service.RollbackAsync(InternalConversationGroupType.Technical, new()
        { ExpectedVersion = 2, RevisionId = v1.Value.ActiveRevisionId!.Value, Reason = "Restore original rules" }, default);
        Assert.Equal(MembershipRequestStatus.Success, restored.Status);
        Assert.Equal(3, restored.Value!.Version);
        Assert.Single(restored.Value.Rules);
        var history = await service.RevisionsAsync(InternalConversationGroupType.Technical, 50, null, default);
        Assert.Equal(new[] { 3, 2, 1 }, history.Value!.Select(r => r.Version));
        Assert.Equal(v1.Value.ActiveRevisionId, history.Value.First().RestoredFromRevisionId);
        Assert.Equal(before, await ProtectedRowsAsync());
    }

    [PostgresGroupStorageFact]
    public async Task MembershipConfigurationRejectsCrossCompanyCategoryAndCrossPolicyRollback()
    {
        await PolicySchemaAsync();
        await using var connection = await postgres.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        var request = PolicyRequest(0);
        request.Rules = [new(DefaultMembershipRuleSource.CompanyRole, PolicyRole, null, DefaultMembershipCategoryScope.Specific, ForeignCategory)];
        var service = PolicyService(db);
        Assert.Equal(MembershipRequestStatus.Invalid, (await service.PublishAsync(InternalConversationGroupType.Technical, request, default)).Status);
        Assert.Empty(await db.DefaultMembershipPolicies.ToArrayAsync());
        var technical = await service.PublishAsync(InternalConversationGroupType.Technical, PolicyRequest(0), default);
        var pricing = await service.PublishAsync(InternalConversationGroupType.Pricing, PolicyRequest(0), default);
        var result = await service.RollbackAsync(InternalConversationGroupType.Technical, new()
        { ExpectedVersion = 1, RevisionId = pricing.Value!.ActiveRevisionId!.Value, Reason = "Invalid cross-policy restore" }, default);
        Assert.Equal(MembershipRequestStatus.NotFound, result.Status);
        Assert.Equal(2, await db.DefaultMembershipRevisions.CountAsync());
        var options = await service.OptionsAsync(default);
        Assert.Contains(options.Value!.ProductCategories, c => c.Id == LocalCategory);
        Assert.DoesNotContain(options.Value.ProductCategories, c => c.Id == ForeignCategory);
        Assert.Contains("QAQC.RD", options.Value.LabGroupTypeCodes);
        Assert.DoesNotContain("FOREIGN.LAB", options.Value.LabGroupTypeCodes);
    }

    [PostgresGroupStorageFact]
    public async Task MembershipConfigurationConcurrentPublicationReturnsOneConflict()
    {
        await PolicySchemaAsync();
        async Task<MembershipRequestResult<DefaultMembershipPolicyDto>> Publish()
        {
            await using var connection = await postgres.OpenAsync();
            await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
            return await PolicyService(db).PublishAsync(InternalConversationGroupType.Technical, PolicyRequest(0), default);
        }
        var result = await Task.WhenAll(Publish(), Publish());
        Assert.Single(result.Where(r => r.Status == MembershipRequestStatus.Success));
        Assert.Single(result.Where(r => r.Status == MembershipRequestStatus.Conflict));
        Assert.Equal(1L, await ScalarAsync<long>("SELECT count(*) FROM \"InternalMail\".\"DefaultMembershipRevisions\""));
    }

    [PostgresGroupStorageFact]
    public async Task MembershipConfigurationPublishedRulesCannotBeEditedOrExtended()
    {
        await PolicySchemaAsync();
        await using var connection = await postgres.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        var published = await PolicyService(db).PublishAsync(InternalConversationGroupType.Technical, PolicyRequest(0), default);
        Assert.Equal(MembershipRequestStatus.Success, published.Status);
        var rule = await db.DefaultMembershipRules.SingleAsync();
        rule.GroupTypeCode = "ILLEGAL";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        var revision = await db.DefaultMembershipRevisions.SingleAsync();
        revision.Reason = "Rewrite historical reason";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        db.DefaultMembershipRules.Add(new() { RuleId = Guid.NewGuid(), CompanyId = Guid.Parse(Company),
            PolicyId = revision.PolicyId, RevisionId = revision.RevisionId, SourceType = DefaultMembershipRuleSource.CompanyRole, RoleId = PolicyRole });
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [PostgresGroupStorageFact]
    public async Task MembershipConfigurationDatabaseRejectsWrongActivePolicyAndDuplicateNullScopedRule()
    {
        await PolicySchemaAsync();
        await using var connection = await postgres.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        var service = PolicyService(db);
        var technical = (await service.PublishAsync(InternalConversationGroupType.Technical, PolicyRequest(0), default)).Value!;
        var pricing = (await service.PublishAsync(InternalConversationGroupType.Pricing, PolicyRequest(0), default)).Value!;
        var wrongPolicy = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "InternalMail"."DefaultMembershipPolicies" SET "ActiveRevisionId"={pricing.ActiveRevisionId}
            WHERE "PolicyId"={technical.PolicyId}
            """));
        Assert.Equal("23503", wrongPolicy.SqlState);
        var duplicate = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "InternalMail"."DefaultMembershipRules"
            ("RuleId","CompanyId","PolicyId","RevisionId","SourceType","RoleId","GroupTypeCode","CategoryScope","ProductCategoryId")
            VALUES ({Guid.NewGuid()},{Guid.Parse(Company)},{technical.PolicyId},{technical.ActiveRevisionId},0,{PolicyRole},NULL,0,NULL)
            """));
        Assert.Equal("23505", duplicate.SqlState);
    }
}
