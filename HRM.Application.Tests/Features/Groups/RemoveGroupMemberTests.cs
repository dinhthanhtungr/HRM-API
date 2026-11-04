using HRM.Application.Abstractions.Persistence.Employees;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.Groups.Commands;
using HRM.Application.Features.Groups.Commands.RemoveGroupMember;
using HRM.Application.Features.Groups.Queries.GetGroupMembers;
using HRM.Application.Features.Groups.Queries.GetGroups;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.HrSchema;

namespace HRM.Application.Tests.Features.Groups;

public sealed class RemoveGroupMemberTests
{
    [Theory]
    [InlineData("Admin", false, false, true, GroupCommandError.None)]
    [InlineData("President", true, true, true, GroupCommandError.None)]
    [InlineData("Developer", true, true, true, GroupCommandError.None)]
    [InlineData("Leader", false, true, true, GroupCommandError.None)]
    [InlineData("Leader", true, true, true, GroupCommandError.Forbidden)]
    [InlineData("Leader", false, false, true, GroupCommandError.Forbidden)]
    [InlineData("SaleUser", false, false, true, GroupCommandError.Forbidden)]
    [InlineData("Admin", false, false, false, GroupCommandError.Forbidden)]
    public async Task Existing_permission_matrix_is_preserved(string role, bool targetLeader,
        bool callerLeader, bool authenticated, GroupCommandError expected)
    {
        var f = new Fixture(role, targetLeader, callerLeader, authenticated);
        var result = await f.Remove();
        Assert.Equal(expected, result.Error);
        Assert.Equal(expected == GroupCommandError.None ? 1 : 0, f.Store.Saves);
        Assert.Equal(expected != GroupCommandError.None, f.Target.IsActive);
        Assert.True(f.Target.ProfileNavigation!.IsActive);
        Assert.Empty(f.Store.Removed);
    }

    [Theory]
    [InlineData("group")]
    [InlineData("employee")]
    [InlineData("missing")]
    [InlineData("inactive")]
    public async Task Missing_or_cross_company_membership_cannot_be_removed(string scenario)
    {
        var f = new Fixture();
        if (scenario == "group") f.Group.CompanyId = Guid.NewGuid();
        if (scenario == "employee") f.Target.ProfileNavigation!.CompanyId = Guid.NewGuid();
        if (scenario == "missing") f.Target.Profile = Guid.NewGuid();
        if (scenario == "inactive") f.Target.IsActive = false;
        Assert.Equal(GroupCommandError.NotFound, (await f.Remove()).Error);
        Assert.Equal(0, f.Store.Saves);
    }

    [Fact]
    public async Task Cross_company_leader_link_does_not_grant_management()
    {
        var f = new Fixture("Leader");
        f.Caller.ProfileNavigation!.CompanyId = Guid.NewGuid();
        Assert.Equal(GroupCommandError.Forbidden, (await f.Remove()).Error);
        Assert.Equal(0, f.Store.Saves);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Counts_exclude_inactive_or_cross_company_employees(bool inactive)
    {
        var f = new Fixture();
        if (inactive) f.Target.ProfileNavigation!.IsActive = false;
        else f.Target.ProfileNavigation!.CompanyId = Guid.NewGuid();
        var (read, store) = ProductionOrderTestStore.CreateContext<IEmployeeReadDbContext>();
        store.Seed("Groups", f.Group);
        store.Seed("MemberInGroups", f.Target, f.Caller);
        var members = await new GetGroupMembersQueryHandler(read, f.User)
            .Handle(new GetGroupMembersQuery(f.Group.GroupId), default);
        var groups = await new GetGroupsQueryHandler(read, f.User).Handle(new GetGroupsQuery(), default);
        Assert.Single(members!.Members);
        Assert.Equal(1, groups.Single().MemberCount);
        Assert.Equal(1, groups.Single().LeaderCount);
    }

    [Fact]
    public async Task Inactive_employee_leader_cannot_replace_last_active_leader()
    {
        var f = new Fixture(targetLeader: true);
        f.Caller.ProfileNavigation!.IsActive = false;
        Assert.Equal(GroupCommandError.Conflict, (await f.Remove()).Error);
        Assert.Equal(0, f.Store.Saves);
    }

    [Fact]
    public async Task Last_leader_is_protected()
    {
        var f = new Fixture(targetLeader: true);
        f.Caller.IsAdmin = false;
        Assert.Equal(GroupCommandError.Conflict, (await f.Remove()).Error);
        Assert.True(f.Target.IsActive);
        Assert.Equal(0, f.Store.Saves);
    }

    [Fact]
    public async Task Removal_updates_reads_and_repeat_returns_not_found()
    {
        var f = new Fixture(targetLeader: true);
        var result = await f.Remove();
        Assert.True(result.Success);
        Assert.False(result.Data!.IsActive);
        Assert.False(result.Data.IsLeader);
        var (read, store) = ProductionOrderTestStore.CreateContext<IEmployeeReadDbContext>();
        store.Seed("Groups", f.Group);
        store.Seed("MemberInGroups", f.Target, f.Caller);
        var members = await new GetGroupMembersQueryHandler(read, f.User)
            .Handle(new GetGroupMembersQuery(f.Group.GroupId), default);
        var groups = await new GetGroupsQueryHandler(read, f.User).Handle(new GetGroupsQuery(), default);
        Assert.Single(members!.Members);
        Assert.Equal(members.Members.Count, groups.Single().MemberCount);
        Assert.Equal(members.Members.Count(m => m.IsLeader), groups.Single().LeaderCount);
        Assert.Equal(GroupCommandError.NotFound, (await f.Remove()).Error);
        Assert.Equal(1, f.Store.Saves);
    }

    private sealed class Fixture
    {
        public Group Group { get; }
        public MemberInGroup Target { get; }
        public MemberInGroup Caller { get; }
        public User User { get; }
        public ProductionOrderTestStore Store { get; }
        private readonly IEmployeeManagementDbContext _db;
        private readonly Guid _targetId = Guid.NewGuid();
        public Fixture(string role = "Admin", bool targetLeader = false,
            bool callerLeader = true, bool authenticated = true)
        {
            var companyId = Guid.NewGuid();
            User = new User(companyId, Guid.NewGuid(), role, authenticated);
            Group = new Group { GroupId = Guid.NewGuid(), CompanyId = companyId, ExternalId = "GRP" };
            Target = Member(_targetId, targetLeader);
            Caller = Member(User.EmployeeId!.Value, callerLeader);
            Group.MemberInGroups = [Target, Caller];
            (_db, Store) = ProductionOrderTestStore.CreateContext<IEmployeeManagementDbContext>();
            Store.Seed("Groups", Group);
            Store.Seed("MemberInGroups", Target, Caller);
        }
        private MemberInGroup Member(Guid id, bool leader) => new()
        {
            MemberId = Guid.NewGuid(), GroupId = Group.GroupId, Group = Group,
            Profile = id, IsAdmin = leader, IsActive = true,
            ProfileNavigation = new Employee { EmployeeId = id, CompanyId = User.CompanyId,
                FullName = "Member", ExternalId = "EMP", IsActive = true }
        };
        public Task<GroupCommandResult<HRM.Application.Features.Groups.Dtos.GroupMemberDto>> Remove()
            => new RemoveGroupMemberCommandHandler(_db, User)
                .Handle(new RemoveGroupMemberCommand(Group.GroupId, _targetId), default);
    }

    private sealed record User(Guid? CompanyId, Guid? EmployeeId, string Role, bool IsAuthenticated) : ICurrentUser
    {
        public Guid UserId => Guid.NewGuid();
        public string? UserName => "test";
        public string? Email => null;
        public IReadOnlyCollection<string> Roles => [Role];
        public bool IsInRole(string role) => role == Role;
    }
}
