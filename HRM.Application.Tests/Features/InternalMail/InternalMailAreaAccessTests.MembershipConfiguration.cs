using HRM.Application.Commons.Authorization;
using HRM.Application.Features.InternalMail.DefaultMembership;
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
    [InlineData("President", false)]
    [InlineData("SaleUser", false)]
    [InlineData("LabUser", false)]
    [InlineData("PLPUUser", false)]
    [InlineData("Admin", false)]
    [InlineData("Developer", true)]
    public async Task MembershipConfigurationRejectsUnprivilegedAndOldExplicitTokensBeforeDatabase(string role, bool explicitToken)
    {
        var user = new User(role) { HasExplicitPermissionSet = explicitToken, Permissions = [] };
        var store = CreateStore(user, InternalMailRelatedType.Internal, false);
        ((MemoryStore)(object)store.Db).Values[nameof(store.Db.DefaultMembershipPolicies)] = null!;
        var service = new DefaultMembershipPolicyService(store.Db, user, new CurrentUserPermissionService(user), new SendTestClock());
        Assert.Equal(MembershipRequestStatus.Forbidden, (await service.PoliciesAsync(default)).Status);
        Assert.Equal(MembershipRequestStatus.Forbidden, (await service.OptionsAsync(default)).Status);
        Assert.Equal(MembershipRequestStatus.Forbidden, (await service.RevisionsAsync(InternalConversationGroupType.Technical, 50, null, default)).Status);
        Assert.Equal(MembershipRequestStatus.Forbidden, (await service.PublishAsync(InternalConversationGroupType.Technical, new(), default)).Status);
        Assert.Equal(MembershipRequestStatus.Forbidden, (await service.RollbackAsync(InternalConversationGroupType.Technical, new(), default)).Status);
    }

    [Theory]
    [InlineData("anonymous")]
    [InlineData("no-company")]
    [InlineData("no-employee")]
    public async Task MembershipConfigurationFailsClosedOnMissingActorContext(string kind)
    {
        var user = new User("Developer") { IsAuthenticated = kind != "anonymous",
            CompanyId = kind == "no-company" ? null : Guid.NewGuid(), EmployeeId = kind == "no-employee" ? null : Guid.NewGuid() };
        var store = CreateStore(new User("Developer"), InternalMailRelatedType.Internal, false);
        var service = new DefaultMembershipPolicyService(store.Db, user, new CurrentUserPermissionService(user), new SendTestClock());
        Assert.Equal(MembershipRequestStatus.Forbidden, (await service.PoliciesAsync(default)).Status);
    }

    [Theory]
    [InlineData("unknown-source")]
    [InlineData("unknown-scope")]
    [InlineData("missing-role")]
    [InlineData("unexpected-code")]
    [InlineData("missing-category")]
    [InlineData("unexpected-category")]
    [InlineData("role-fallback")]
    [InlineData("lab-role")]
    [InlineData("lab-code")]
    [InlineData("long-code")]
    [InlineData("duplicate")]
    public void MembershipConfigurationRuleValidationRejectsAmbiguousSources(string failure)
    {
        var role = Guid.NewGuid();
        var rule = new DefaultMembershipRuleDto(DefaultMembershipRuleSource.CompanyRole, role, null, DefaultMembershipCategoryScope.All, null);
        rule = failure switch
        {
            "unknown-source" => rule with { SourceType = (DefaultMembershipRuleSource)99 },
            "unknown-scope" => rule with { CategoryScope = (DefaultMembershipCategoryScope)99 },
            "missing-role" => rule with { RoleId = null },
            "unexpected-code" => rule with { GroupTypeCode = "LAB" },
            "missing-category" => rule with { CategoryScope = DefaultMembershipCategoryScope.Specific },
            "unexpected-category" => rule with { ProductCategoryId = Guid.NewGuid() },
            "role-fallback" => rule with { CategoryScope = DefaultMembershipCategoryScope.Fallback },
            "lab-role" => rule with { SourceType = DefaultMembershipRuleSource.LabGroupLeader, GroupTypeCode = "QAQC.RD" },
            "lab-code" => rule with { SourceType = DefaultMembershipRuleSource.LabGroupLeader, RoleId = null, GroupTypeCode = "invalid code" },
            "long-code" => rule with { SourceType = DefaultMembershipRuleSource.LabGroupLeader, RoleId = null, GroupTypeCode = new string('X',65) },
            _ => rule
        };
        Assert.NotNull(DefaultMembershipRuleValidation.Normalize(failure == "duplicate" ? [rule, rule] : [rule], out _));
    }

    [Fact]
    public void MembershipConfigurationNormalizesLabCodesAndKeepsEmptyPolicyExplicit()
    {
        Assert.Null(DefaultMembershipRuleValidation.Normalize([], out var empty));
        Assert.Empty(empty);
        DefaultMembershipRuleDto[] input = [new(DefaultMembershipRuleSource.LabGroupLeader, null, "  qaqc.rd  ", DefaultMembershipCategoryScope.Fallback, null)];
        Assert.Null(DefaultMembershipRuleValidation.Normalize(input, out var normalized));
        Assert.Equal("QAQC.RD", Assert.Single(normalized).GroupTypeCode);
        Assert.Equal("  qaqc.rd  ", input[0].GroupTypeCode);
        Assert.NotNull(DefaultMembershipRuleValidation.Normalize([input[0], input[0] with { GroupTypeCode = "QAQC.RD" }], out _));
    }

    [Fact]
    public void MembershipConfigurationModelHasThreeScopedTablesAndNoShadowFields()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var model = db.GetService<IDesignTimeModel>().Model;
        foreach (var type in new[] { typeof(DefaultMembershipPolicy), typeof(DefaultMembershipRevision), typeof(DefaultMembershipRule) })
        {
            var entity = model.FindEntityType(type)!;
            Assert.Equal("InternalMail", entity.GetSchema());
            Assert.True(entity.IsTableExcludedFromMigrations());
            Assert.DoesNotContain(entity.GetProperties(), p => p.IsShadowProperty());
            Assert.All(entity.GetForeignKeys(), fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
        }
        Assert.True(model.FindEntityType(typeof(DefaultMembershipPolicy))!.FindProperty(nameof(DefaultMembershipPolicy.Version))!.IsConcurrencyToken);
        var activeFk = model.FindEntityType(typeof(DefaultMembershipPolicy))!.GetForeignKeys().Single(f => f.PrincipalEntityType.ClrType == typeof(DefaultMembershipRevision));
        Assert.Equal(new[] { "CompanyId", "PolicyId", "ActiveRevisionId" }, activeFk.Properties.Select(p => p.Name));
    }
}
