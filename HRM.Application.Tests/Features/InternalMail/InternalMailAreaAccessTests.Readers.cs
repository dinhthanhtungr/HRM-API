using HRM.Application.Features.InternalMail.Dtos;
using HRM.Application.Features.InternalMail.Services;
using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Enums.InternalMailEnums;
using HRM.Domain.Identity;

namespace HRM.Application.Tests.Features.InternalMail;

public sealed partial class InternalMailAreaAccessTests
{
    [Theory]
    [InlineData(true, true, true, false, true)]
    [InlineData(true, true, true, true, true)]
    [InlineData(false, true, true, false, false)]
    [InlineData(true, false, true, false, false)]
    [InlineData(true, true, false, false, false)]
    public async Task UnmappedLabUsersFallBackToCommonLabWhileLeaderStaysSeparate(
        bool activeRole, bool sameCompany, bool activeEmployee, bool hasRdMembership, bool grouped)
    {
        var user = new User("SaleUser");
        var store = CreateStore(user, InternalMailRelatedType.ConversationCustom, true);
        var company = user.CompanyId!.Value;
        var leader = new InternalConversationParticipantDto { EmployeeId = Guid.NewGuid(), EmployeeName = "Lab Leader", IsLeader = true };
        var member = new InternalConversationParticipantDto { EmployeeId = Guid.NewGuid(), EmployeeName = "Lab Member" };
        var sale = new InternalConversationParticipantDto { EmployeeId = Guid.NewGuid(), EmployeeName = "Sale", IsLeader = true };
        var hiddenId = Guid.NewGuid();
        var role = new ApplicationRole { Id = Guid.NewGuid(), Name = "LabUser" };
        var accounts = new[] { leader.EmployeeId, member.EmployeeId, hiddenId }
            .Select(id => new ApplicationUser { Id = Guid.NewGuid(), EmployeeId = id }).ToArray();
        var memory = (MemoryStore)(object)store.Db;
        memory.Values["Employees"] = new MemorySet<Employee>([
            new() { EmployeeId = leader.EmployeeId, CompanyId = company, IsActive = true },
            new() { EmployeeId = member.EmployeeId, CompanyId = sameCompany ? company : Guid.NewGuid(), IsActive = activeEmployee },
            new() { EmployeeId = hiddenId, CompanyId = company, IsActive = true }
        ]);
        memory.Values["Users"] = new MemorySet<ApplicationUser>(accounts);
        memory.Values["Roles"] = new MemorySet<ApplicationRole>([role]);
        memory.Values["UserRoles"] = new MemorySet<ApplicationUserRole>(accounts.Select(account => new ApplicationUserRole
        {
            UserId = account.Id, RoleId = role.Id, IsActive = account.EmployeeId != member.EmployeeId || activeRole
        }));
        var rdId = Guid.NewGuid();
        if (hasRdMembership)
            memory.Values["MemberInGroups"] = new MemorySet<MemberInGroup>([ReaderMembership(member.EmployeeId, company, rdId)]);
        var result = await new InternalMailAreaAccessService(store.Db, user).WithReaderGroupsAsync([sale, member, leader], default);
        Assert.Equal(3, result.Count);
        Assert.Equal(leader.EmployeeId, result[0].EmployeeId);
        Assert.Null(leader.ReaderGroup);
        Assert.Null(sale.ReaderGroup);
        Assert.Equal(grouped, member.ReaderGroup is not null);
        if (grouped)
        {
            Assert.Equal(hasRdMembership ? rdId : Guid.Empty, member.ReaderGroup!.GroupId);
            Assert.Equal(hasRdMembership ? "QAQC.RD" : "LAB", member.ReaderGroup.GroupType);
            Assert.Equal(hasRdMembership ? (Guid?)null : leader.EmployeeId, member.ReaderGroup.LeaderEmployeeId);
        }
    }

    private static MemberInGroup ReaderMembership(Guid employee, Guid company, Guid group, string type = "QAQC.RD", bool admin = false) => new()
    {
        MemberId = Guid.NewGuid(), Profile = employee, GroupId = group, IsAdmin = admin, IsActive = true,
        Group = new Group { GroupId = group, GroupType = type, CompanyId = company },
        ProfileNavigation = new Employee { EmployeeId = employee, CompanyId = company, IsActive = true }
    };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReaderGroupsKeepLabLeadersSeparateAndNeverAddNonParticipants(bool roleLeader)
    {
        var user = new User("SaleUser");
        var store = CreateStore(user, InternalMailRelatedType.ConversationCustom, true);
        var company = user.CompanyId!.Value;
        var group = Guid.NewGuid();
        var leader = new InternalConversationParticipantDto { EmployeeId = Guid.NewGuid(), EmployeeName = "Leader", IsLeader = roleLeader, JoinedAt = new DateTime(2026, 3, 1) };
        var member = new InternalConversationParticipantDto { EmployeeId = Guid.NewGuid(), EmployeeName = "Lab", JoinedAt = new DateTime(2026, 1, 1) };
        var sale = new InternalConversationParticipantDto { EmployeeId = Guid.NewGuid(), EmployeeName = "Sale" };
        ((MemoryStore)(object)store.Db).Values["MemberInGroups"] = new MemorySet<MemberInGroup>([
            ReaderMembership(leader.EmployeeId, company, group, admin: !roleLeader),
            ReaderMembership(member.EmployeeId, company, group),
            ReaderMembership(Guid.NewGuid(), company, group)
        ]);
        var result = await new InternalMailAreaAccessService(store.Db, user).WithReaderGroupsAsync([sale, member, leader], default);
        Assert.Equal(3, result.Count);
        Assert.Equal(leader.EmployeeId, result[0].EmployeeId);
        Assert.Null(leader.ReaderGroup);
        Assert.Null(sale.ReaderGroup);
        Assert.Equal(group, member.ReaderGroup!.GroupId);
        Assert.Equal(leader.EmployeeId, member.ReaderGroup.LeaderEmployeeId);
    }

    [Theory]
    [InlineData("QAQC.RD", true, true, true, true)]
    [InlineData("QAQC.MAU", true, true, true, true)]
    [InlineData("SALE", true, true, true, false)]
    [InlineData("QAQC.RD", false, true, true, false)]
    [InlineData("QAQC.RD", true, false, true, false)]
    [InlineData("QAQC.RD", true, true, false, false)]
    public async Task ReaderGroupingRequiresActiveSameCompanyLabMembership(string type, bool active, bool sameCompany, bool employeeActive, bool grouped)
    {
        var user = new User("SaleUser");
        var store = CreateStore(user, InternalMailRelatedType.ConversationCustom, true);
        var member = new InternalConversationParticipantDto { EmployeeId = Guid.NewGuid() };
        var membership = ReaderMembership(member.EmployeeId, user.CompanyId!.Value, Guid.NewGuid(), type);
        membership.IsActive = active;
        membership.ProfileNavigation!.IsActive = employeeActive;
        if (!sameCompany) membership.Group.CompanyId = Guid.NewGuid();
        ((MemoryStore)(object)store.Db).Values["MemberInGroups"] = new MemorySet<MemberInGroup>([membership]);
        await new InternalMailAreaAccessService(store.Db, user).WithReaderGroupsAsync([member], default);
        Assert.Equal(grouped, member.ReaderGroup is not null);
    }

    [Fact]
    public async Task ReaderGroupDoesNotExposeLeaderWhoIsOutsideVisibleParticipants()
    {
        var user = new User("SaleUser");
        var store = CreateStore(user, InternalMailRelatedType.ConversationPricing, true);
        var group = Guid.NewGuid();
        var member = new InternalConversationParticipantDto { EmployeeId = Guid.NewGuid() };
        ((MemoryStore)(object)store.Db).Values["MemberInGroups"] = new MemorySet<MemberInGroup>([
            ReaderMembership(member.EmployeeId, user.CompanyId!.Value, group),
            ReaderMembership(Guid.NewGuid(), user.CompanyId.Value, group, admin: true)
        ]);
        var result = await new InternalMailAreaAccessService(store.Db, user).WithReaderGroupsAsync([member], default);
        Assert.Single(result);
        Assert.Null(result[0].ReaderGroup!.LeaderEmployeeId);
    }

    [Fact]
    public async Task MultipleLabMembershipsAssignEachVisiblePersonToOnlyOneStableGroup()
    {
        var user = new User("SaleUser");
        var store = CreateStore(user, InternalMailRelatedType.ConversationCustom, true);
        var member = new InternalConversationParticipantDto { EmployeeId = Guid.NewGuid() };
        var first = Guid.Parse("00000001-0000-0000-0000-000000000000");
        var second = Guid.Parse("00000002-0000-0000-0000-000000000000");
        var mappings = new[] { ReaderMembership(member.EmployeeId, user.CompanyId!.Value, second, "QAQC.MAU"), ReaderMembership(member.EmployeeId, user.CompanyId.Value, first) };
        var memory = (MemoryStore)(object)store.Db;
        memory.Values["MemberInGroups"] = new MemorySet<MemberInGroup>(mappings);
        var access = new InternalMailAreaAccessService(store.Db, user);
        await access.WithReaderGroupsAsync([member], default);
        Assert.Equal(first, member.ReaderGroup!.GroupId);
        memory.Values["MemberInGroups"] = new MemorySet<MemberInGroup>(mappings.Reverse());
        await access.WithReaderGroupsAsync([member], default);
        Assert.Equal(first, member.ReaderGroup!.GroupId);
    }
}
