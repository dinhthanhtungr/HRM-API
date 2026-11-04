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
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task VisibleParticipantsPrioritizesOnlyActiveCompanyLeaders(bool membershipActive, bool sameCompany)
    {
        var user = new User("SaleUser");
        var store = CreateStore(user, InternalMailRelatedType.ConversationCustom, true);
        var memberId = Guid.NewGuid();
        var leaderId = Guid.NewGuid();
        var account = new ApplicationUser { Id = Guid.NewGuid(), EmployeeId = leaderId };
        var role = new ApplicationRole { Id = Guid.NewGuid(), Name = "Leader" };
        var memory = (MemoryStore)(object)store.Db;
        memory.Values["Employees"] = new MemorySet<Employee>([new()
        {
            EmployeeId = leaderId, CompanyId = sameCompany ? user.CompanyId!.Value : Guid.NewGuid(), IsActive = true
        }]);
        memory.Values["Users"] = new MemorySet<ApplicationUser>([account]);
        memory.Values["Roles"] = new MemorySet<ApplicationRole>([role]);
        memory.Values["UserRoles"] = new MemorySet<ApplicationUserRole>([new()
        {
            UserId = account.Id, RoleId = role.Id, IsActive = membershipActive
        }]);
        var result = await new InternalMailAreaAccessService(store.Db, user).VisibleParticipantsAsync(
            InternalMailRelatedType.ConversationCustom,
            [new() { EmployeeId = memberId, JoinedAt = new DateTime(2026, 1, 1) },
             new() { EmployeeId = leaderId, JoinedAt = new DateTime(2026, 2, 1) }], default);
        Assert.Equal(membershipActive && sameCompany ? leaderId : memberId, result[0].EmployeeId);
        Assert.Equal(2, result.Count);
    }

    [Theory]
    [InlineData("SaleUser", true, false)]
    [InlineData("Developer", false, true)]
    [InlineData("Developer", true, true)]
    public void PricingObservationSupportsCompanyDeveloperAndLegacyPermission(string role, bool permission, bool expected)
    {
        var user = new User(role) { HasExplicitPermissionSet = true,
            Permissions = permission ? [HRM.Application.Commons.Authorization.ApplicationPermissions.InternalMail.ObservePricingGroup] : [] };
        var store = CreateStore(user, InternalMailRelatedType.ConversationPricing, false);
        store.Root.RelatedType = InternalMailRelatedType.SampleRequest;
        Assert.Equal(expected, new InternalMailAreaAccessService(store.Db, user).Conversations()
            .Any(c => c.InternalConversationId == store.Child.InternalConversationId));
    }

    [Theory]
    [InlineData(InternalMailRelatedType.SampleRequest, false)]
    [InlineData(InternalMailRelatedType.ConversationPricing, false)]
    [InlineData(InternalMailRelatedType.ConversationTechnical, true)]
    [InlineData(InternalMailRelatedType.ConversationCustom, true)]
    public async Task RestrictedForwardCannotExposeContentToImplicitReaders(InternalMailRelatedType target, bool expected)
    {
        var user = new User("SaleUser");
        var store = CreateStore(user, InternalMailRelatedType.ConversationCustom, true);
        var access = new InternalMailAreaAccessService(store.Db, user);
        Assert.Equal(expected, await access.IsForwardDestinationBoundedAsync(target, [store.Child.InternalConversationId], default));
        Assert.True(await access.IsForwardDestinationBoundedAsync(target, [store.Root.InternalConversationId], default));
    }

    [Fact]
    public async Task AutomaticDefaultMembershipCannotWidenExistingForwardAudience()
    {
        var user = new User("SaleUser");
        var store = CreateStore(user, InternalMailRelatedType.ConversationTechnical, true);
        var allowed = new Employee { EmployeeId = user.EmployeeId!.Value, CompanyId = user.CompanyId!.Value, IsActive = true };
        var candidate = Guid.NewGuid();
        var participant = new InternalConversationParticipant { InternalConversationId = store.Root.InternalConversationId,
            EmployeeId = allowed.EmployeeId, Employee = allowed, IsActive = true };
        var memory = (MemoryStore)(object)store.Db;
        memory.Values["Employees"] = new MemorySet<Employee>([allowed]);
        memory.Values["InternalConversationParticipants"] = new MemorySet<InternalConversationParticipant>([participant]);
        memory.Values["InternalMessageReferences"] = new MemorySet<InternalMessageReference>([new()
        {
            Message = new InternalMessage { InternalConversationId = store.Child.InternalConversationId },
            RelatedType = InternalMailRelatedType.ConversationForwardSource, RelatedId = store.Root.InternalConversationId
        }]);
        var access = new InternalMailAreaAccessService(store.Db, user);
        Assert.Equal([allowed.EmployeeId], await access.ConstrainDefaultMembersToForwardSourcesAsync(store.Child.InternalConversationId, [allowed.EmployeeId, candidate], default));
        participant.IsActive = false;
        Assert.Empty(await access.ConstrainDefaultMembersToForwardSourcesAsync(store.Child.InternalConversationId, [allowed.EmployeeId, candidate], default));
    }

    [Fact]
    public async Task DefaultPricingIncludesOnlyConversationSalesAndCompanyPresident()
    {
        var user = new User("SaleUser");
        var store = CreateStore(user, InternalMailRelatedType.ConversationPricing, false);
        store.Root.RelatedType = InternalMailRelatedType.SampleRequest;
        var company = user.CompanyId!.Value;
        var sale = new Employee { EmployeeId = Guid.NewGuid(), CompanyId = company, IsActive = true };
        var outsider = new Employee { EmployeeId = Guid.NewGuid(), CompanyId = company, IsActive = true };
        var president = new Employee { EmployeeId = Guid.NewGuid(), CompanyId = company, IsActive = true };
        var foreign = new Employee { EmployeeId = Guid.NewGuid(), CompanyId = Guid.NewGuid(), IsActive = true };
        var employees = new[] { sale, outsider, president, foreign };
        var accounts = employees.Select(e => new ApplicationUser { Id = Guid.NewGuid(), EmployeeId = e.EmployeeId }).ToArray();
        var saleRole = new ApplicationRole { Id = Guid.NewGuid(), Name = "SaleUser" };
        var presidentRole = new ApplicationRole { Id = Guid.NewGuid(), Name = "President" };
        var memory = (MemoryStore)(object)store.Db;
        memory.Values["Employees"] = new MemorySet<Employee>(employees);
        memory.Values["Users"] = new MemorySet<ApplicationUser>(accounts);
        memory.Values["Roles"] = new MemorySet<ApplicationRole>([saleRole, presidentRole]);
        memory.Values["UserRoles"] = new MemorySet<ApplicationUserRole>(accounts.Select((account, index) => new ApplicationUserRole
        { UserId = account.Id, RoleId = index < 2 ? saleRole.Id : presidentRole.Id, IsActive = true }));
        memory.Values["InternalConversationParticipants"] = new MemorySet<InternalConversationParticipant>([
            new() { InternalConversationId = store.Root.InternalConversationId, EmployeeId = sale.EmployeeId, IsActive = true }]);
        var result = await new InternalMailAreaAccessService(store.Db, user).DefaultMembersAsync(store.Root.InternalConversationId, "pricing", default);
        Assert.Equal(2, result.Length);
        Assert.Contains(sale.EmployeeId, result);
        Assert.Contains(president.EmployeeId, result);
        Assert.DoesNotContain(outsider.EmployeeId, result);
        Assert.DoesNotContain(foreign.EmployeeId, result);
        var developerRole = new ApplicationRole { Id = Guid.NewGuid(), Name = "Developer" };
        memory.Values["Roles"] = new MemorySet<ApplicationRole>([saleRole, presidentRole, developerRole]);
        memory.Values["UserRoles"] = new MemorySet<ApplicationUserRole>([
            new() { UserId = accounts[0].Id, RoleId = saleRole.Id, IsActive = true },
            new() { UserId = accounts[0].Id, RoleId = developerRole.Id, IsActive = true },
            new() { UserId = accounts[2].Id, RoleId = presidentRole.Id, IsActive = true }]);
        var observerDefaults = await new InternalMailAreaAccessService(store.Db, user).DefaultMembersAsync(store.Root.InternalConversationId, "pricing", default);
        Assert.DoesNotContain(sale.EmployeeId, observerDefaults);
        Assert.Contains(president.EmployeeId, observerDefaults);
    }

    [Theory]
    [InlineData("CMP", true, false)]
    [InlineData("PIG", false, true)]
    [InlineData("GCO", true, true)]
    public async Task TechnicalDefaultsKeepProductLeaderMapping(string category, bool rdExpected, bool colourExpected)
    {
        var user = new User("SaleUser");
        var store = CreateStore(user, InternalMailRelatedType.ConversationTechnical, false);
        var company = user.CompanyId!.Value;
        var request = new SampleRequest { SampleRequestId = Guid.NewGuid(), CompanyId = company, IsActive = true,
            Product = new() { CompanyId = company, Category = new() { CompanyId = company, ExternalId = category } } };
        store.Root.RelatedType = InternalMailRelatedType.SampleRequest;
        store.Root.RelatedId = request.SampleRequestId;
        var rd = new Employee { EmployeeId = Guid.NewGuid(), CompanyId = company, IsActive = true };
        var colour = new Employee { EmployeeId = Guid.NewGuid(), CompanyId = company, IsActive = true };
        var memory = (MemoryStore)(object)store.Db;
        memory.Values["SampleRequests"] = new MemorySet<SampleRequest>([request]);
        memory.Values["Employees"] = new MemorySet<Employee>([rd, colour]);
        memory.Values["MemberInGroups"] = new MemorySet<MemberInGroup>([
            new() { IsActive = true, IsAdmin = true, Profile = rd.EmployeeId, ProfileNavigation = rd, Group = new Group { CompanyId = company, GroupType = "QAQC.RD" } },
            new() { IsActive = true, IsAdmin = true, Profile = colour.EmployeeId, ProfileNavigation = colour, Group = new Group { CompanyId = company, GroupType = "QAQC.MAU" } }]);
        var result = await new InternalMailAreaAccessService(store.Db, user).DefaultMembersAsync(store.Root.InternalConversationId, "technical", default);
        Assert.Equal(rdExpected, result.Contains(rd.EmployeeId));
        Assert.Equal(colourExpected, result.Contains(colour.EmployeeId));
    }

    [Theory]
    [InlineData("technical")]
    [InlineData("pricing")]
    public async Task DefaultGroupsIncludeActiveCompanyPresidentOnly(string area)
    {
        var user = new User("Developer");
        var store = CreateStore(user, InternalMailRelatedType.ConversationTechnical, false);
        store.Root.RelatedType = InternalMailRelatedType.SampleRequest;
        var company = user.CompanyId!.Value;
        var employees = new[]
        {
            new Employee { EmployeeId = Guid.NewGuid(), CompanyId = company, IsActive = true },
            new Employee { EmployeeId = Guid.NewGuid(), CompanyId = company, IsActive = false },
            new Employee { EmployeeId = Guid.NewGuid(), CompanyId = Guid.NewGuid(), IsActive = true },
            new Employee { EmployeeId = Guid.NewGuid(), CompanyId = company, IsActive = true }
        };
        var accounts = employees.Select(e => new ApplicationUser { Id = Guid.NewGuid(), EmployeeId = e.EmployeeId }).ToArray();
        var role = new ApplicationRole { Id = Guid.NewGuid(), Name = "President" };
        var memory = (MemoryStore)(object)store.Db;
        memory.Values["Employees"] = new MemorySet<Employee>(employees);
        memory.Values["Users"] = new MemorySet<ApplicationUser>(accounts);
        memory.Values["Roles"] = new MemorySet<ApplicationRole>([role]);
        memory.Values["UserRoles"] = new MemorySet<ApplicationUserRole>(accounts.Select((account, index) =>
            new ApplicationUserRole { UserId = account.Id, RoleId = role.Id, IsActive = index != 3 }));
        var result = await new InternalMailAreaAccessService(store.Db, user).DefaultMembersAsync(store.Root.InternalConversationId, area, default);
        Assert.Equal([employees[0].EmployeeId], result);
    }

    [Theory]
    [InlineData("Developer", true)]
    [InlineData("President", false)]
    [InlineData("SaleUser", false)]
    [InlineData("LabUser", false)]
    public void SampleGroupObserversReadAllGroupsOnlyWithinActiveCompanyRoot(string role, bool expected)
    {
        var user = new User(role);
        var store = CreateStore(user, InternalMailRelatedType.ConversationPricing, false);
        store.Root.RelatedType = InternalMailRelatedType.SampleRequest;
        var access = new InternalMailAreaAccessService(store.Db, user);
        Assert.Equal(expected, access.Conversations().Any(c => c.InternalConversationId == store.Child.InternalConversationId));
        Assert.Empty(store.Child.Participants);
        store.Child.RelatedType = InternalMailRelatedType.ConversationCustom;
        Assert.Equal(expected, access.Conversations().Any(c => c.InternalConversationId == store.Child.InternalConversationId));
        store.Child.RelatedType = InternalMailRelatedType.ConversationTechnical;
        Assert.Equal(expected, access.Conversations().Any(c => c.InternalConversationId == store.Child.InternalConversationId));
        Assert.Equal(expected, access.Messages().Any());
        store.Root.RelatedType = InternalMailRelatedType.Internal;
        Assert.Equal(role == "Developer", access.Conversations().Any(c => c.InternalConversationId == store.Child.InternalConversationId));
        store.Root.RelatedType = InternalMailRelatedType.SampleRequest;
        user.IsAuthenticated = false;
        Assert.Empty(access.Conversations());
        user.IsAuthenticated = true;
        store.Child.RelatedType = InternalMailRelatedType.ConversationPricing;
        store.Root.IsActive = false;
        Assert.DoesNotContain(access.Conversations(), c => c.InternalConversationId == store.Child.InternalConversationId);
        store.Root.IsActive = true;
        user.CompanyId = Guid.NewGuid();
        Assert.Empty(access.Conversations());
    }

    [Theory]
    [InlineData("Developer", true)]
    [InlineData("President", false)]
    [InlineData("SaleUser", false)]
    [InlineData("LabUser", false)]
    public void SampleGroupObservationHonorsExplicitPermissionAndRole(string role, bool allowed)
    {
        var user = new User(role) { HasExplicitPermissionSet = true };
        var store = CreateStore(user, InternalMailRelatedType.ConversationTechnical, false);
        store.Root.RelatedType = InternalMailRelatedType.SampleRequest;
        var access = new InternalMailAreaAccessService(store.Db, user);
        Assert.Equal(role == "Developer", access.Messages().Any());
        user.Permissions = [HRM.Application.Commons.Authorization.ApplicationPermissions.InternalMail.ObserveSampleRequestGroups];
        Assert.Equal(allowed, access.Messages().Any());
        Assert.Empty(store.Child.Participants);
        user.Permissions = ["unknown.permission"];
        Assert.Equal(role == "Developer", access.Messages().Any());
    }

    [Fact]
    public void CustomGroupKeepsIndependentMembershipAndAreaScope()
    {
        var user = new User("SaleUser");
        var store = CreateStore(user, InternalMailRelatedType.ConversationCustom, true);
        var access = new InternalMailAreaAccessService(store.Db, user);
        Assert.Single(access.Messages(InternalMailAreas.Custom));
        Assert.Empty(access.Messages(InternalMailAreas.General));
        Assert.Empty(access.Messages(InternalMailAreas.Technical));
        store.Child.Participants.Single().IsActive = false;
        Assert.Empty(access.Messages());
    }

    [Fact]
    public async Task ForwardingRejectsWiderAudienceAndRevokedSourceMembership()
    {
        var user = new User("SaleUser");
        var store = CreateStore(user, InternalMailRelatedType.ConversationTechnical, true);
        var employee = new Employee { EmployeeId = user.EmployeeId!.Value, CompanyId = user.CompanyId!.Value, IsActive = true };
        var participant = store.Child.Participants.Single();
        participant.InternalConversationId = store.Child.InternalConversationId;
        participant.Employee = employee;
        var memory = (MemoryStore)(object)store.Db;
        memory.Values["Employees"] = new MemorySet<Employee>([employee]);
        memory.Values["InternalConversationParticipants"] = new MemorySet<InternalConversationParticipant>([participant]);
        var access = new InternalMailAreaAccessService(store.Db, user);
        Assert.True(await access.CanForwardToMembersAsync(store.Child.InternalConversationId, [employee.EmployeeId], default));
        Assert.False(await access.CanForwardToMembersAsync(store.Child.InternalConversationId, [employee.EmployeeId, Guid.NewGuid()], default));
        participant.IsActive = false;
        Assert.False(await access.CanForwardToMembersAsync(store.Child.InternalConversationId, [employee.EmployeeId], default));
    }
}
