using HRM.Application.Features.InternalMail.Services;
using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Enums.InternalMailEnums;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HRM.Application.Tests.Features.InternalMail;

public sealed partial class InternalMailAreaAccessTests
{
    [Theory]
    [InlineData("LabUser", true, true)]
    [InlineData("LabUser", false, false)]
    [InlineData("SaleUser", false, false)]
    [InlineData("Developer", false, true)]
    public async Task ResearchMentionPreviewBeforeFirstMessageIsScopedAndReadOnly(string role, bool member, bool allowed)
    {
        var user = new User(role);
        var store = CreateStore(user, InternalMailRelatedType.ConversationTechnical, false);
        store.Root.RelatedType = InternalMailRelatedType.SampleRequest;
        var employee = new Employee { EmployeeId = member ? user.EmployeeId!.Value : Guid.NewGuid(),
            CompanyId = user.CompanyId!.Value, IsActive = true, FullName = "Research member" };
        var inactive = new Employee { EmployeeId = Guid.NewGuid(), CompanyId = user.CompanyId.Value, IsActive = false };
        var foreign = new Employee { EmployeeId = Guid.NewGuid(), CompanyId = Guid.NewGuid(), IsActive = true };
        var memory = (MemoryStore)(object)store.Db;
        memory.Values[nameof(store.Db.Employees)] = new MemorySet<Employee>([employee, inactive, foreign]);
        memory.Values[nameof(store.Db.MemberInGroups)] = new MemorySet<MemberInGroup>(
            new[] { employee, inactive, foreign }.Select(e => new MemberInGroup {
                Profile = e.EmployeeId, ProfileNavigation = e, IsActive = true,
                Group = new Group { CompanyId = e.CompanyId, GroupType = "QAQC.RD" } }));
        var access = new InternalMailAreaAccessService(store.Db, user);
        var result = await access.PreviewResearchParticipantsAsync(store.Root.InternalConversationId, default);
        if (allowed) Assert.Equal(employee.EmployeeId, Assert.Single(result!).EmployeeId);
        else Assert.Null(result);
        Assert.Equal(2, await store.Db.InternalConversations.CountAsync());
        Assert.Empty(store.Db.InternalConversationParticipants);
        Assert.Null(await access.PreviewResearchParticipantsAsync(Guid.NewGuid(), default));
    }

    [Fact]
    public async Task VirtualResearchTabDoesNotCreateGroupAndDisappearsOncePersisted()
    {
        var user = new User("Developer");
        var store = CreateStore(user, InternalMailRelatedType.ConversationCustom, false);
        store.Root.RelatedType = InternalMailRelatedType.SampleRequest;
        var access = new InternalMailAreaAccessService(store.Db, user);
        var dto = new HRM.Application.Features.InternalMail.Dtos.InternalConversationDetailDto
            { ConversationId = store.Root.InternalConversationId, RelatedType = store.Root.RelatedType };
        var tab = Assert.Single(await access.DescribeAsync(dto, default), a => a.Code == "rd");
        Assert.Null(tab.ConversationId);
        Assert.True(tab.CanSend);
        Assert.Equal(2, await store.Db.InternalConversations.CountAsync());
        store.Child.RelatedExternalId = InternalMailAreaAccessService.ResearchMarker;
        Assert.DoesNotContain(await access.DescribeAsync(dto, default), a => a.Code == "rd");
    }

    [Fact]
    public void ResearchQueriesTranslateWithoutConnectingToDatabase()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var user = new User("LabUser");
        var access = new InternalMailAreaAccessService(db, user);
        Assert.Contains("QAQC.RD", access.Conversations().ToQueryString());
        Assert.Contains("QAQC.RD", access.Messages().ToQueryString());
        Assert.Contains("QAQC.RD", access.Recipients(user.CompanyId!.Value, "custom", Guid.NewGuid()).ToQueryString());
    }

    [Theory]
    [InlineData("LabUser", true, true, true)]
    [InlineData("LabUser", false, true, false)]
    [InlineData("President", false, true, false)]
    [InlineData("SaleUser", false, true, false)]
    [InlineData("Developer", false, false, true)]
    [InlineData("LabUser", true, false, true)]
    public async Task ResearchScopeRequiresCurrentRdTeamOrExistingDeveloperObserver(
        string role, bool rd, bool participant, bool expected)
    {
        var user = new User(role);
        var store = CreateStore(user, InternalMailRelatedType.ConversationCustom, participant);
        store.Root.RelatedType = InternalMailRelatedType.SampleRequest;
        store.Child.RelatedExternalId = InternalMailAreaAccessService.ResearchMarker;
        var member = new MemberInGroup
        {
            Profile = user.EmployeeId, IsActive = true,
            ProfileNavigation = new Employee { EmployeeId = user.EmployeeId!.Value, CompanyId = user.CompanyId, IsActive = true },
            Group = new Group { CompanyId = user.CompanyId, GroupType = rd ? "QAQC.RD" : "QAQC.Color" }
        };
        ((MemoryStore)(object)store.Db).Values[nameof(store.Db.MemberInGroups)] = new MemorySet<MemberInGroup>([member]);
        var access = new InternalMailAreaAccessService(store.Db, user);
        Assert.Equal(expected, await access.Conversations().AnyAsync(c => c.InternalConversationId == store.Child.InternalConversationId));
        Assert.Equal(expected, await access.Messages().AnyAsync());
        member.IsActive = false;
        Assert.Equal(role == "Developer", await access.Conversations().AnyAsync(c => c.InternalConversationId == store.Child.InternalConversationId));
    }

    [Fact]
    public async Task ResearchRecipientsExcludeOtherTeamsAndCompanies()
    {
        var user = new User("Developer");
        var store = CreateStore(user, InternalMailRelatedType.ConversationCustom, false);
        store.Child.RelatedExternalId = InternalMailAreaAccessService.ResearchMarker;
        var employee = new Employee { EmployeeId = Guid.NewGuid(), CompanyId = user.CompanyId, IsActive = true };
        var member = new MemberInGroup { Profile = employee.EmployeeId, ProfileNavigation = employee,
            Group = new Group { CompanyId = user.CompanyId, GroupType = "QAQC.RD" } };
        var memory = (MemoryStore)(object)store.Db;
        memory.Values[nameof(store.Db.MemberInGroups)] = new MemorySet<MemberInGroup>([member]);
        memory.Values[nameof(store.Db.Employees)] = new MemorySet<Employee>([employee]);
        var access = new InternalMailAreaAccessService(store.Db, user);
        Assert.Contains(employee.EmployeeId, await access.Recipients(user.CompanyId!.Value, "custom", store.Child.InternalConversationId).ToArrayAsync());
        member.Group.GroupType = "QAQC.Color";
        Assert.Empty(await access.Recipients(user.CompanyId.Value, "custom", store.Child.InternalConversationId).ToArrayAsync());
        member.Group.GroupType = "QAQC.RD";
        member.Group.CompanyId = Guid.NewGuid();
        Assert.Empty(await access.Recipients(user.CompanyId.Value, "custom", store.Child.InternalConversationId).ToArrayAsync());
    }

    [Fact]
    public async Task OrdinaryCustomGroupNamedResearchKeepsExistingMembershipRules()
    {
        var user = new User("SaleUser");
        var store = CreateStore(user, InternalMailRelatedType.ConversationCustom, true);
        store.Child.Subject = "R&D";
        Assert.True(await new InternalMailAreaAccessService(store.Db, user).Conversations()
            .AnyAsync(c => c.InternalConversationId == store.Child.InternalConversationId));
    }
}
