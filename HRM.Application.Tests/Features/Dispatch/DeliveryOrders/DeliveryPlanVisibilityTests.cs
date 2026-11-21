using HRM.Application.Commons.Authorization;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.Dispatch.DeliveryOrders;
using HRM.Application.Abstractions.Security;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.OrderSchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using HRM.Application.Features.Dispatch.DeliveryOrders.Queries.GetDeliveryPlan;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.Dispatch.DeliveryOrders;

public sealed class DeliveryPlanVisibilityTests
{
    [Fact]
    public void SaleCanFinishWithoutGainingOtherDispatchMutations()
    {
        var user = new Viewer { Roles = ["SaleUser"] };
        Assert.True(DeliveryOrderAccessRules.CanFinish(user));
        Assert.False(DeliveryOrderAccessRules.CanManage(user));
        Assert.False(DeliveryOrderAccessRules.CanCancel(user));
        Assert.False(DeliveryOrderAccessRules.CanFinish(new Viewer { Roles = ["ACCUser"] }));
        Assert.False(DeliveryOrderAccessRules.CanFinish(new Viewer { IsAuthenticated = false, Roles = ["SaleUser"] }));
        Assert.Contains("SaleUser", DeliveryOrderAccessRules.FinishRoles.Split(','));
    }

    [Theory]
    [InlineData("QCUser")]
    [InlineData("User")]
    public async Task UnrelatedRoleCannotFinishBeforeReadingRecords(string role)
    {
        var user = new Viewer { Roles = [role] };
        var finish = new HRM.Application.Features.Dispatch.DeliveryOrders.Commands.FinishDelivery.FinishDeliveryCommandHandler(null!, user, null!, null!, null!);
        Assert.False((await finish.Handle(new(Guid.NewGuid()), default)).Success);
    }

    [Fact]
    public async Task AnonymousUserCannotPauseBeforeReadingRecords()
    {
        var user = new Viewer { IsAuthenticated = false, Roles = ["SaleUser"] };
        var pause = new HRM.Application.Features.PLM.SaleOrders.Commands.PauseSaleOrderDelivery.PauseSaleOrderDeliveryCommandHandler(null!, user, null!, null!, null!);
        Assert.False((await pause.Handle(new(new()), default)).Success);
    }
    [Theory]
    [InlineData("SaleUser", true)]
    [InlineData("Leader", true)]
    [InlineData("DispatchUser", false)]
    [InlineData("Developer", false)]
    [InlineData("PLPUUser", false)]
    [InlineData("User", false)]
    public void CustomerScopeIsRequiredOnlyForAuthorizedSalesReaders(string role, bool expected)
        => Assert.Equal(expected, DeliveryOrderAccessRules.RequiresCustomerScopeForPlan(new Viewer { Roles = [role] }));

    [Fact]
    public void UnauthenticatedReaderCannotOpenPlan()
        => Assert.False(DeliveryOrderAccessRules.CanRead(new Viewer { IsAuthenticated = false, Roles = ["SaleUser"] }));

    [Fact]
    public void SalesUsesCustomerAssignmentScopeBeforeCountingAndPaging()
    {
        var company = Guid.NewGuid();
        var employee = Guid.NewGuid();
        var own = new Customer { CustomerId = Guid.NewGuid(), CompanyId = company, IsActive = true };
        own.CustomerAssignments.Add(new CustomerAssignment { EmployeeId = employee, IsActive = true });
        var other = new Customer { CustomerId = Guid.NewGuid(), CompanyId = company, IsActive = true };
        var foreign = new Customer { CustomerId = Guid.NewGuid(), CompanyId = Guid.NewGuid(), IsActive = true };
        foreign.CustomerAssignments.Add(new CustomerAssignment { EmployeeId = employee, IsActive = true });
        var orders = new[] { own, other, foreign }.Select(c => new MerchandiseOrder
        {
            MerchandiseOrderId = Guid.NewGuid(), CustomerId = c.CustomerId, CompanyId = c.CompanyId, IsActive = true
        }).AsQueryable();
        var scope = new ViewerScope(company, employee, false, false, new HashSet<Guid>(), new HashSet<Guid> { employee }, DateTime.UtcNow);
        var service = new CustomerVisibilityService(null!, null!, null!);
        var visible = service.ApplyMerchandiseOrderVisibility(orders, new[] { own, other, foreign }.AsQueryable(), scope);
        Assert.Equal(1, visible.Count());
        Assert.Equal(own.CustomerId, Assert.Single(visible.OrderBy(x => x.MerchandiseOrderId).Skip(0).Take(15)).CustomerId);
        Assert.Empty(visible.Where(x => x.CustomerId == other.CustomerId || x.CustomerId == foreign.CustomerId));
    }

    [Fact]
    public void ScopedPlanTranslatesToPostgresBeforePaging()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var employee = Guid.NewGuid();
        var company = Guid.NewGuid();
        var scope = new ViewerScope(company, employee, false, false, new HashSet<Guid>(), new HashSet<Guid> { employee }, DateTime.UtcNow);
        var service = new CustomerVisibilityService(null!, null!, null!);
        var orders = service.ApplyMerchandiseOrderVisibility(db.MerchandiseOrders.AsNoTracking(), db.Customers.AsNoTracking(), scope);
        var sql = DeliveryPlanSourceQuery.Project(orders, db.MfgOrderPOs.AsNoTracking(), db.DeliveryOrderDetails.AsNoTracking(), company, db.Employees.AsNoTracking())
            .OrderByDescending(x => x.ExternalId).ThenByDescending(x => x.Id).Skip(15).Take(15).ToQueryString();
        Assert.Contains(db.Model.FindEntityType(typeof(CustomerAssignment))!.GetTableName()!, sql);
        Assert.Contains(employee.ToString(), sql);
        Assert.Contains("LIMIT", sql);
        Assert.Contains("OFFSET", sql);
    }

    private sealed class Viewer : ICurrentUser
    {
        public bool IsAuthenticated { get; init; } = true;
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid? EmployeeId { get; } = Guid.NewGuid();
        public Guid? CompanyId { get; } = Guid.NewGuid();
        public string? UserName => null;
        public string? Email => null;
        public IReadOnlyCollection<string> Roles { get; init; } = [];
        public bool IsInRole(string role) => Roles.Contains(role);
        public bool IsInAnyRole(params string[] roles) => roles.Any(IsInRole);
    }
}
