using System.Reflection;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Features.PLM.SampleRequests.Services;
using HRM.Application.Tests.Features.InternalMail;
using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.Notifications;
using HRM.Domain.Identity;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleRequestNotificationAudienceResolverTests
{
    [Fact]
    public async Task SalesComeFromActiveCustomerAssignmentsAndAdminsFromTheAssignedGroup()
    {
        var company = Guid.NewGuid();
        var customer = new Customer { CustomerId = Guid.NewGuid(), CompanyId = company, IsActive = true };
        var sale = Employee(company);
        var admin = Employee(company);
        var otherAdmin = Employee(company);
        var managerOnRequest = Employee(company);
        var group = new Group { GroupId = Guid.NewGuid(), CompanyId = company };
        var otherGroup = new Group { GroupId = Guid.NewGuid(), CompanyId = company };
        var db = DispatchProxy.Create<IInternalMailDbContext, InternalMailAreaAccessTests.MemoryStore>();
        var crm = DispatchProxy.Create<ICRMReadDbContext, InternalMailAreaAccessTests.MemoryStore>();
        var request = new SampleRequest
        {
            SampleRequestId = Guid.NewGuid(), CustomerId = customer.CustomerId, CompanyId = company, IsActive = true,
            ManagerBy = managerOnRequest.EmployeeId,
            Product = new() { CompanyId = company, Category = new() { CompanyId = company, ExternalId = "PIG" } }
        };
        Set(db, "SampleRequests", request);
        Set(db, "Employees", sale, admin, otherAdmin, managerOnRequest);
        Set(crm, "CustomerAssignments", new CustomerAssignment
        {
            CompanyId = company, IsActive = true, CustomerId = customer.CustomerId, Customer = customer,
            EmployeeId = sale.EmployeeId, Employee = sale, GroupId = group.GroupId, Group = group
        });
        var role = new ApplicationRole { Id = Guid.NewGuid(), Name = "SaleAdmin", NormalizedName = "SALEADMIN" };
        var adminUser = new ApplicationUser { Id = Guid.NewGuid(), EmployeeId = admin.EmployeeId };
        var otherUser = new ApplicationUser { Id = Guid.NewGuid(), EmployeeId = otherAdmin.EmployeeId };
        Set(db, "Roles", role);
        Set(db, "Users", adminUser, otherUser);
        Set(db, "UserRoles", new ApplicationUserRole { UserId = adminUser.Id, RoleId = role.Id },
            new ApplicationUserRole { UserId = otherUser.Id, RoleId = role.Id });
        Set(db, "MemberInGroups", new MemberInGroup { GroupId = group.GroupId, Group = group, Profile = admin.EmployeeId },
            new MemberInGroup { GroupId = otherGroup.GroupId, Group = otherGroup, Profile = otherAdmin.EmployeeId });
        var resolver = new SampleRequestNotificationAudienceResolver(crm, db);
        var recipients = await resolver.ResolveAsync(company, request.SampleRequestId, TopicNotifications.SampleRequestSampleSent, default);
        Assert.Equal(new[] { sale.EmployeeId, admin.EmployeeId }.Order(), recipients.Order());
        Assert.DoesNotContain(managerOnRequest.EmployeeId, recipients);
        Assert.Empty(await resolver.ResolveAsync(Guid.NewGuid(), request.SampleRequestId, TopicNotifications.SampleRequestSampleSent, default));
        customer.IsActive = false;
        Assert.Empty(await resolver.ResolveAsync(company, request.SampleRequestId, TopicNotifications.SampleRequestSampleSent, default));
    }

    [Theory]
    [InlineData("CMP", true, false)]
    [InlineData("PIG", false, true)]
    [InlineData("GCO", true, true)]
    [InlineData("unknown", true, true)]
    public async Task LabGroupsFollowProductMappingAndLabAdminIsCompanyWide(string category, bool rdExpected, bool colorExpected)
    {
        var company = Guid.NewGuid();
        var rd = Employee(company);
        var color = Employee(company);
        var admin = Employee(company);
        var foreignAdmin = Employee(Guid.NewGuid());
        var ordinaryLab = Employee(company);
        var db = DispatchProxy.Create<IInternalMailDbContext, InternalMailAreaAccessTests.MemoryStore>();
        var crm = DispatchProxy.Create<ICRMReadDbContext, InternalMailAreaAccessTests.MemoryStore>();
        var request = new SampleRequest
        {
            SampleRequestId = Guid.NewGuid(), CompanyId = company, IsActive = true,
            Product = new() { CompanyId = company, Category = new() { CompanyId = company, ExternalId = category } }
        };
        Set(db, "SampleRequests", request);
        Set(db, "Employees", rd, color, admin, foreignAdmin, ordinaryLab);
        var rdGroup = new Group { GroupId = Guid.NewGuid(), CompanyId = company, GroupType = "QAQC.RD" };
        var colorGroup = new Group { GroupId = Guid.NewGuid(), CompanyId = company, GroupType = "QAQC.MAU" };
        Set(db, "MemberInGroups",
            new MemberInGroup { GroupId = rdGroup.GroupId, Group = rdGroup, Profile = rd.EmployeeId, ProfileNavigation = rd, IsAdmin = true },
            new MemberInGroup { GroupId = colorGroup.GroupId, Group = colorGroup, Profile = color.EmployeeId, ProfileNavigation = color, IsAdmin = true },
            new MemberInGroup { GroupId = rdGroup.GroupId, Group = rdGroup, Profile = ordinaryLab.EmployeeId, ProfileNavigation = ordinaryLab, IsAdmin = false });
        var role = new ApplicationRole { Id = Guid.NewGuid(), Name = "LabAdmin" };
        var users = new[] { admin, foreignAdmin }.Select(e => new ApplicationUser { Id = Guid.NewGuid(), EmployeeId = e.EmployeeId }).ToArray();
        Set(db, "Roles", role);
        Set(db, "Users", users);
        Set(db, "UserRoles", users.Select(u => new ApplicationUserRole { UserId = u.Id, RoleId = role.Id }).ToArray());
        var recipients = await new SampleRequestNotificationAudienceResolver(crm, db).ResolveAsync(
            company, request.SampleRequestId, TopicNotifications.SampleRequestFormulaUpdateApproved, default);
        Assert.Equal(rdExpected, recipients.Contains(rd.EmployeeId));
        Assert.Equal(colorExpected, recipients.Contains(color.EmployeeId));
        Assert.Contains(admin.EmployeeId, recipients);
        Assert.DoesNotContain(foreignAdmin.EmployeeId, recipients);
        Assert.DoesNotContain(ordinaryLab.EmployeeId, recipients);
    }

    private static Employee Employee(Guid company) => new() { EmployeeId = Guid.NewGuid(), CompanyId = company, IsActive = true };
    private static void Set<T>(object proxy, string name, params T[] rows) where T : class
        => ((InternalMailAreaAccessTests.MemoryStore)proxy).Values[name] = new InternalMailAreaAccessTests.MemorySet<T>(rows);
}
