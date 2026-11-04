using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Application.Features.PLM.SampleRequests.Services;
using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleLabTeamWorkTests
{
    private static IQueryable<SampleRequest> Samples() => new[] { "PHM", "CMP", "GCO", "OTHER", "" }
        .Select((code, i) => new SampleRequest { ExternalId = i.ToString(), Product = new Product { Category = new HRM.Domain.Entities.MaterialSchema.Category { ExternalId = code } } }).AsQueryable();

    [Theory]
    [InlineData("PHM", "QAQC.MAU")]
    [InlineData("CMP", "QAQC.RD")]
    [InlineData("GCO", "QAQC.RD,QAQC.MAU")]
    [InlineData("unknown", "")]
    [InlineData(null, "")]
    public void CategoryTeamRoutingMatchesNotificationRules(string? category, string expected)
    {
        Assert.Equal(expected, string.Join(',', SampleRequestLabTeamRules.Resolve(category)));
        var notification = SampleRequestRecipientRules.ResolveDefaultLeaderGroupTypes(category);
        Assert.Equal(expected.Length == 0 ? new[] { "QAQC.RD", "QAQC.MAU" } : expected.Split(','), notification);
    }

    [Fact]
    public void MineUsesUnionWithoutDuplicatesAndNoMembershipReturnsNoWork()
    {
        var both = new SampleLabTeamContext(["QAQC.RD", "QAQC.MAU"], false, SampleLabTeamFilter.Mine);
        Assert.Equal(3, SampleRequestLabTeamScope.Apply(Samples(), both, null).Count());
        Assert.Equal(2, SampleRequestLabTeamScope.Apply(Samples(), both with { MemberTeams = ["QAQC.RD"] }, null).Count());
        Assert.Empty(SampleRequestLabTeamScope.Apply(Samples(), both with { MemberTeams = [] }, null));
    }

    [Theory]
    [InlineData(SampleLabTeamFilter.All)]
    [InlineData(SampleLabTeamFilter.Unassigned)]
    [InlineData(SampleLabTeamFilter.Color)]
    public void TeamMembershipDoesNotRestrictExplicitSelection(SampleLabTeamFilter filter)
    {
        var result = SampleRequestLabTeamScope.Apply(Samples(),
            new(["QAQC.RD"], false, SampleLabTeamFilter.Mine), filter);
        Assert.Equal(filter == SampleLabTeamFilter.All ? 5 : 2, result.Count());
    }

    [Fact]
    public void AllPreservesExistingCompanyAndActiveScope()
    {
        var company = Guid.NewGuid();
        var rows = new[] {
            new SampleRequest { CompanyId = company, IsActive = true },
            new SampleRequest { CompanyId = Guid.NewGuid(), IsActive = true },
            new SampleRequest { CompanyId = company, IsActive = false }
        }.AsQueryable();
        var scoped = rows.Where(x => x.CompanyId == company && x.IsActive);
        Assert.Single(SampleRequestLabTeamScope.Apply(scoped,
            new(["QAQC.RD"], true, SampleLabTeamFilter.Mine), SampleLabTeamFilter.All));
    }

    [Fact]
    public void LabAdministratorCanSeeUnassignedAndAll()
    {
        var admin = new SampleLabTeamContext([], true, SampleLabTeamFilter.All);
        Assert.Equal(5, SampleRequestLabTeamScope.Apply(Samples(), admin, null).Count());
        Assert.Equal(2, SampleRequestLabTeamScope.Apply(Samples(), admin, SampleLabTeamFilter.Unassigned).Count());
        Assert.Throws<ArgumentException>(() => SampleRequestLabTeamScope.Apply(Samples(), admin, (SampleLabTeamFilter)999));
        Assert.Contains("\"All\"", System.Text.Json.JsonSerializer.Serialize(admin));
    }

    [Fact]
    public void MembershipRequiresCompanyEmployeeAndActiveMembership()
    {
        var company = Guid.NewGuid(); var employee = Guid.NewGuid();
        MemberInGroup Member(Guid tenant, Guid person, bool active, string team) => new() {
            Group = new Group { CompanyId = tenant, GroupType = team }, Profile = person, IsActive = active };
        var members = new[] { Member(company, employee, true, "QAQC.RD"), Member(company, employee, false, "QAQC.MAU"),
            Member(Guid.NewGuid(), employee, true, "QAQC.MAU"), Member(company, Guid.NewGuid(), true, "QAQC.MAU"),
            Member(company, employee, true, "OTHER") }.AsQueryable();
        Assert.Equal(new[] { "QAQC.RD" }, SampleRequestLabTeamScope.MemberTeams(members, company, employee));
    }

    [Theory]
    [InlineData("LabAdmin", true)]
    [InlineData("LabUser", true)]
    [InlineData("SaleUser", false)]
    [InlineData("Admin", false)]
    [InlineData("Developer", false)]
    [InlineData("President", false)]
    public void ViewAllCapabilityIsLimitedToLabRoles(string role, bool allowed)
    {
        var user = new SampleTrialFeedbackCorrectionTests.User(role);
        Assert.Equal(allowed, new CurrentUserPermissionService(user).HasPermission(ApplicationPermissions.PLM.ViewAllLabTeamWork));
    }

    [Fact]
    public async Task AnonymousCannotReadMembership()
    {
        var user = new SampleTrialFeedbackCorrectionTests.User("LabUser", false);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            new SampleRequestLabTeamScope(null!, user).ReadAsync(CancellationToken.None));
    }

    [Fact]
    public void PostgresTranslatesTeamScopeBeforePagination()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var company = Guid.NewGuid();
        var scoped = db.SampleRequests.Where(x => x.CompanyId == company && x.IsActive);
        var sql = SampleRequestLabTeamScope.Apply(scoped, new(["QAQC.RD"], false, SampleLabTeamFilter.Mine), null)
            .OrderBy(x => x.SampleRequestId).Take(15).ToQueryString();
        Assert.Contains("LIMIT", sql); Assert.Contains("CompanyId", sql); Assert.Contains("ANY", sql);
        Assert.Contains("upper", sql, StringComparison.OrdinalIgnoreCase);
        var members = SampleRequestLabTeamScope.MemberTeams(db.MemberInGroups, company, Guid.NewGuid()).ToQueryString();
        Assert.Contains("DISTINCT", members); Assert.Contains("CompanyId", members); Assert.Contains("IsActive", members);
    }
}
