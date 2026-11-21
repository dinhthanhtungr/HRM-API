using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;
using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleWorkSaleFilterTests
{
    [Fact]
    public void GroupUnionAndEmployeeIntersectionPreserveCompanyAndActiveMembership()
    {
        var company = Guid.NewGuid(); var groupA = Guid.NewGuid(); var groupB = Guid.NewGuid();
        var a = Guid.NewGuid(); var b = Guid.NewGuid(); var excluded = Guid.NewGuid();
        MemberInGroup Member(Guid employee, Guid group, bool active = true, Guid? tenant = null) => new() {
            Profile = employee, GroupId = group, IsActive = active,
            Group = new() { CompanyId = tenant ?? company, GroupType = "CMR.HCM" },
            ProfileNavigation = new Employee { EmployeeId = employee, CompanyId = company, IsActive = true }
        };
        var members = new[] { Member(a, groupA), Member(b, groupB), Member(excluded, groupA, false),
            Member(excluded, groupB, tenant: Guid.NewGuid()) }.AsQueryable();
        var rows = new[] { a, b, excluded }.Select(id => new SampleRequest { ManagerBy = id, CompanyId = company, IsActive = true }).AsQueryable();
        Assert.Equal(2, SampleWorkSaleFilter.Apply(rows, members, company, [groupA, groupB], null).Count());
        Assert.Equal(a, Assert.Single(SampleWorkSaleFilter.Apply(rows, members, company, [groupA, groupB], [a])).ManagerBy);
        Assert.Empty(SampleWorkSaleFilter.Apply(rows, members, Guid.NewGuid(), null, [a]));
        Assert.Empty(SampleWorkSaleFilter.Apply(rows, members, company, [Guid.NewGuid()], [a]));
    }

    [Fact]
    public void InvalidIdsAndOversizedFiltersAreRejected()
    {
        var rows = Array.Empty<SampleRequest>().AsQueryable(); var members = Array.Empty<MemberInGroup>().AsQueryable();
        Assert.Throws<ArgumentException>(() => SampleWorkSaleFilter.Apply(rows, members, Guid.NewGuid(), [Guid.Empty], null));
        Assert.Throws<ArgumentException>(() => SampleWorkSaleFilter.Apply(rows, members, Guid.NewGuid(), null, Enumerable.Range(0, 101).Select(_ => Guid.NewGuid()).ToArray()));
    }

    [Fact]
    public void FiltersTranslateBeforePaginationWithoutMultiplyingRows()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var sql = SampleWorkSaleFilter.Apply(db.SampleRequests, db.MemberInGroups, Guid.NewGuid(), [Guid.NewGuid()], [Guid.NewGuid()])
            .OrderBy(x => x.SampleRequestId).Take(15).ToQueryString();
        Assert.Contains("EXISTS", sql); Assert.Contains("CompanyId", sql); Assert.Contains("LIMIT", sql);
    }
}
