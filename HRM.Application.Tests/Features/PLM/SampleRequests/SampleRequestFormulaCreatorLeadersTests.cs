using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;
using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.HrSchema;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleRequestFormulaCreatorLeadersTests
{
    [Fact]
    public void Leaders_AreActiveAdminsOfCreatorsActualGroupsWithinCompany()
    {
        var companyId = Guid.NewGuid();
        var creator = new Employee { EmployeeId = Guid.NewGuid(), CompanyId = companyId, IsActive = true };
        var group = new Group { GroupId = Guid.NewGuid(), CompanyId = companyId };
        var other = new Group { GroupId = Guid.NewGuid(), CompanyId = companyId };
        var foreign = new Group { GroupId = Guid.NewGuid(), CompanyId = Guid.NewGuid() };
        MemberInGroup Member(Group target, Employee employee, bool isLeader = false) => new()
        {
            GroupId = target.GroupId, Group = target, Profile = employee.EmployeeId,
            ProfileNavigation = employee, IsAdmin = isLeader, IsActive = true
        };
        Employee Leader(string name, Guid? company = null, bool active = true) => new()
        {
            EmployeeId = Guid.NewGuid(), FullName = name, CompanyId = company ?? companyId, IsActive = active
        };
        var members = new[]
        {
            Member(group, creator), Member(group, Leader("Leader A"), true), Member(group, Leader("Leader B"), true),
            Member(other, Leader("Unrelated"), true), Member(group, Leader("Member")),
            Member(group, Leader("Inactive", active: false), true),
            Member(group, Leader("Wrong company", Guid.NewGuid()), true),
            Member(foreign, creator), Member(foreign, Leader("Foreign group"), true)
        };

        var result = SampleRequestFormulaCreatorLeaders.Build(members.AsQueryable(), companyId, [creator.EmployeeId])
            .OrderBy(x => x.LeaderName).ToArray();

        Assert.Equal(new[] { "Leader A", "Leader B" }, result.Select(x => x.LeaderName));
        Assert.All(result, row => Assert.Equal(creator.EmployeeId, row.CreatorEmployeeId));
    }
}
