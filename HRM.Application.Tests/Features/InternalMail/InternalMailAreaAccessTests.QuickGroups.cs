using HRM.Application.Features.InternalMail.Services;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Enums.InternalMailEnums;
using HRM.Domain.Identity;
using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.SampleRequestSchema;

namespace HRM.Application.Tests.Features.InternalMail;

public sealed partial class InternalMailAreaAccessTests
{
    [Fact]
    public async Task QuickGroupUnreadCountsStayWithinVisibleGroups()
    {
        var user = new User("SaleUser");
        var store = CreateStore(user, InternalMailRelatedType.ConversationTechnical, true);
        store.Root.RelatedType = InternalMailRelatedType.SampleRequest;
        store.Root.Participants.Add(new() { EmployeeId = user.EmployeeId!.Value, IsActive = true });
        ((MemoryStore)(object)store.Db).Values[nameof(store.Db.QueryInternalConversationNotificationUnreadCounts)] =
            new AsyncQuery<HRM.Application.Abstractions.Persistence.InternalMail.InternalConversationNotificationUnreadCount>([
                new() { ConversationId = store.Child.InternalConversationId, UnreadCount = 4 },
                new() { ConversationId = store.Root.InternalConversationId, UnreadCount = 7 }
            ]);
        var access = new InternalMailAreaAccessService(store.Db, user);
        var groups = (await access.QuickGroupsAsync([store.Root.InternalConversationId], default))[store.Root.InternalConversationId];
        Assert.Equal(4, Assert.Single(groups, g => g.Code == "technical").UnreadCount);
        store.Child.Participants.Single().IsActive = false;
        groups = (await access.QuickGroupsAsync([store.Root.InternalConversationId], default))[store.Root.InternalConversationId];
        Assert.DoesNotContain(groups, g => g.Code == "technical");
        Assert.All(groups, g => Assert.Equal(0, g.UnreadCount));
    }

    [Theory]
    [InlineData("CMP", "QAQC.RD", true)]
    [InlineData("CMP", "QAQC.MAU", false)]
    [InlineData("PIG", "QAQC.RD", false)]
    [InlineData("PIG", "QAQC.MAU", true)]
    public async Task QuickGroupsRespectLeaderCategory(string category, string groupType, bool expected)
    {
        var user = new User("LabUser");
        var store = CreateStore(user, InternalMailRelatedType.ConversationPricing, false);
        store.Root.RelatedType = InternalMailRelatedType.SampleRequest;
        store.Root.Participants.Add(new() { EmployeeId = user.EmployeeId!.Value, IsActive = true });
        var company = user.CompanyId!.Value;
        var employee = new Employee { EmployeeId = user.EmployeeId.Value, CompanyId = company, IsActive = true };
        var request = new SampleRequest { SampleRequestId = Guid.NewGuid(), CompanyId = company, IsActive = true,
            Product = new() { CompanyId = company, Category = new() { CompanyId = company, ExternalId = category } } };
        store.Root.RelatedId = request.SampleRequestId;
        var memory = (MemoryStore)(object)store.Db;
        memory.Values["SampleRequests"] = new MemorySet<SampleRequest>([request]);
        memory.Values["Employees"] = new MemorySet<Employee>([employee]);
        memory.Values["MemberInGroups"] = new MemorySet<MemberInGroup>([new() { IsActive = true, IsAdmin = true,
            Profile = employee.EmployeeId, ProfileNavigation = employee, Group = new Group { CompanyId = company, GroupType = groupType } }]);
        var result = await new InternalMailAreaAccessService(store.Db, user).QuickGroupsAsync([store.Root.InternalConversationId], default);
        Assert.Equal(expected, result.Values.SelectMany(g => g).Any(g => g.Code == "technical" && g.ConversationId == null));
    }

    [Theory]
    [InlineData("Developer")]
    [InlineData("President")]
    public async Task QuickGroupsPreviewMissingDefaultsWithoutWriting(string role)
    {
        var user = new User(role);
        var store = CreateStore(user, InternalMailRelatedType.ConversationTechnical, false);
        ((MemoryStore)(object)store.Db).Values[nameof(store.Db.QueryInternalConversationNotificationUnreadCounts)] =
            new AsyncQuery<HRM.Application.Abstractions.Persistence.InternalMail.InternalConversationNotificationUnreadCount>([]);
        store.Root.RelatedType = InternalMailRelatedType.SampleRequest;
        var result = await new InternalMailAreaAccessService(store.Db, user).QuickGroupsAsync([store.Root.InternalConversationId], default);
        if (role == "President")
        {
            Assert.Empty(result); // No implicit root or child membership.
            return;
        }
        var groups = result[store.Root.InternalConversationId];
        Assert.Equal(store.Child.InternalConversationId, groups.Single(g => g.Code == "technical").ConversationId);
        Assert.Null(groups.Single(g => g.Code == "pricing").ConversationId);
        Assert.Equal(2, store.Db.InternalConversations.Count());
        Assert.Empty(store.Child.Participants);
    }

    [Fact]
    public async Task QuickGroupsKeepMembershipAndCompanyBoundaries()
    {
        var user = new User("SaleUser");
        var store = CreateStore(user, InternalMailRelatedType.ConversationTechnical, true);
        ((MemoryStore)(object)store.Db).Values[nameof(store.Db.QueryInternalConversationNotificationUnreadCounts)] =
            new AsyncQuery<HRM.Application.Abstractions.Persistence.InternalMail.InternalConversationNotificationUnreadCount>([]);
        store.Root.RelatedType = InternalMailRelatedType.SampleRequest;
        var access = new InternalMailAreaAccessService(store.Db, user);
        var groups = (await access.QuickGroupsAsync([store.Root.InternalConversationId], default))[store.Root.InternalConversationId];
        Assert.Single(groups);
        Assert.Equal("technical", groups[0].Code);
        store.Child.Participants.Single().IsActive = false;
        Assert.Empty(await access.QuickGroupsAsync([store.Root.InternalConversationId], default));
        store.Child.Participants.Single().IsActive = true;
        user.CompanyId = Guid.NewGuid();
        Assert.Empty(await access.QuickGroupsAsync([store.Root.InternalConversationId], default));
    }

    [Fact]
    public async Task QuickGroupsPreviewOnlySaleDefaultAndNeverRecreateHiddenExistingGroup()
    {
        var user = new User("SaleUser");
        var store = CreateStore(user, InternalMailRelatedType.ConversationTechnical, false);
        store.Root.RelatedType = InternalMailRelatedType.SampleRequest;
        store.Root.Participants.Add(new() { EmployeeId = user.EmployeeId!.Value, IsActive = true });
        var employee = new Employee { EmployeeId = user.EmployeeId.Value, CompanyId = user.CompanyId!.Value, IsActive = true };
        var account = new ApplicationUser { Id = Guid.NewGuid(), EmployeeId = employee.EmployeeId };
        var role = new ApplicationRole { Id = Guid.NewGuid(), Name = "SaleUser" };
        var memory = (MemoryStore)(object)store.Db;
        memory.Values["Employees"] = new MemorySet<Employee>([employee]);
        memory.Values["Users"] = new MemorySet<ApplicationUser>([account]);
        memory.Values["Roles"] = new MemorySet<ApplicationRole>([role]);
        memory.Values["UserRoles"] = new MemorySet<ApplicationUserRole>([new() { UserId = account.Id, RoleId = role.Id, IsActive = true }]);
        var access = new InternalMailAreaAccessService(store.Db, user);
        var groups = (await access.QuickGroupsAsync([store.Root.InternalConversationId], default))[store.Root.InternalConversationId];
        Assert.Single(groups);
        Assert.Equal("pricing", groups[0].Code);
        Assert.Null(groups[0].ConversationId);
        store.Child.RelatedType = InternalMailRelatedType.ConversationPricing;
        Assert.Empty((await access.QuickGroupsAsync([store.Root.InternalConversationId], default))[store.Root.InternalConversationId]);
        user.IsAuthenticated = false;
        Assert.Empty(await access.QuickGroupsAsync([store.Root.InternalConversationId], default));
    }
}
