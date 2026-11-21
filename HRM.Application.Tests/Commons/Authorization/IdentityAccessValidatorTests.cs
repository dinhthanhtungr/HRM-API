using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Identity;
using HRM.Infrastructure.Authentication;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Commons.Authorization;

public sealed class IdentityAccessValidatorTests
{
    [Theory]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public async Task CurrentRoleAssignmentMustMatchToken(bool activeRole, bool tokenRole, bool allowed)
    {
        var company = Guid.NewGuid();
        var employee = new Employee { EmployeeId = Guid.NewGuid(), CompanyId = company, IsActive = true };
        var user = new ApplicationUser { Id = Guid.NewGuid(), EmployeeId = employee.EmployeeId };
        var role = new ApplicationRole { Id = Guid.NewGuid(), Name = "imsuser", NormalizedName = null };
        var (sets, store) = ProductionOrderTestStore.CreateContext<IIdentitySets>();
        store.Seed(nameof(sets.Users), user);
        store.Seed(nameof(sets.Roles), role);
        store.Seed(nameof(sets.UserRoles), new ApplicationUserRole
        {
            UserId = user.Id, RoleId = role.Id, IsActive = activeRole
        });
        store.Seed(nameof(sets.Employees), employee);
        using var db = new IdentityContext(sets);
        var validator = new IdentityAccessValidator(db);

        Assert.Equal(allowed, await validator.IsAccessAllowedAsync(user.Id, employee.EmployeeId, company, tokenRole));
        Assert.False(await validator.IsAccessAllowedAsync(user.Id, employee.EmployeeId, Guid.NewGuid(), tokenRole));
    }

    public interface IIdentitySets
    {
        DbSet<ApplicationUser> Users { get; }
        DbSet<ApplicationRole> Roles { get; }
        DbSet<ApplicationUserRole> UserRoles { get; }
        DbSet<Employee> Employees { get; }
    }

    private sealed class IdentityContext(IIdentitySets sets)
        : ApplicationDbContext(new DbContextOptions<ApplicationDbContext>())
    {
        public override DbSet<ApplicationUser> Users { get => sets.Users; set { } }
        public override DbSet<ApplicationRole> Roles { get => sets.Roles; set { } }
        public override DbSet<ApplicationUserRole> UserRoles { get => sets.UserRoles; set { } }
        public override DbSet<Employee> Employees { get => sets.Employees; set { } }
    }
}
