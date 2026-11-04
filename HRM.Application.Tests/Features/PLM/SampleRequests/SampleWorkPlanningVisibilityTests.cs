using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.SampleRequests.Services;
using HRM.Application.Features.PLM.SampleRequests.Commands.UpdateSampleWorkSchedule;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.SampleRequestSchema;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleWorkPlanningVisibilityTests
{
    [Fact]
    public async Task SaleWithCustomScheduleCapabilityCannotScheduleCustomerOutsideNormalVisibility()
    {
        var user = new SampleTrialFeedbackCorrectionTests.User("SaleUser");
        var (db, store) = ProductionOrderTestStore.CreateContext<IPLMWriteDbContext>();
        var customer = new Customer { CustomerId = Guid.NewGuid(), CompanyId = user.CompanyId!.Value, IsActive = true, ExternalId = "KH_TEST", IsLead = false };
        var sample = new SampleRequest { SampleRequestId = Guid.NewGuid(), CompanyId = user.CompanyId.Value,
            CustomerId = customer.CustomerId, Customer = customer, ManagerBy = Guid.NewGuid(), IsActive = true, Status = "InProgress" };
        store.Seed(nameof(db.Employees), new Employee { EmployeeId = user.EmployeeId!.Value, CompanyId = user.CompanyId.Value, IsActive = true });
        store.Seed(nameof(db.Customers), customer);
        store.Seed(nameof(db.SampleRequests), sample);
        var handler = new UpdateSampleWorkScheduleCommandHandler(db, user, new ScheduleGrant(),
            SampleWorkSchedulePostgresTests.RestrictedVisibility.Create(user), null!);
        var result = await handler.Handle(new() { SampleRequestId = sample.SampleRequestId, ExpectedDeliveryDate = new(2026, 11, 8) }, default);
        Assert.False(result.Success);
        Assert.Equal(0, store.Saves);
        Assert.Empty(store.Added);
    }

    private sealed class ScheduleGrant : ICurrentUserPermissionService
    {
        public bool HasPermission(string permission) => permission == ApplicationPermissions.PLM.ManageSampleWorkSchedule;
    }

    [Theory]
    [InlineData("LabAdmin", true)]
    [InlineData("SaleUser", false)]
    public async Task PlanningIsLocalToActiveLabAdminAndPreservesCompanyBoundary(string role, bool expanded)
    {
        var user = new SampleTrialFeedbackCorrectionTests.User(role);
        var (db, store) = ProductionOrderTestStore.CreateContext<IPLMReadDbContext>();
        var employee = new Employee { EmployeeId = user.EmployeeId!.Value, CompanyId = user.CompanyId!.Value, IsActive = true };
        store.Seed(nameof(db.Employees), employee);
        var original = new ViewerScope(user.CompanyId.Value, user.EmployeeId.Value, false, false,
            new HashSet<Guid>(), new HashSet<Guid>(), DateTime.Now);
        var planning = await SampleWorkPlanningVisibility.ForPlanningAsync(original, user, db.Employees, default);
        Assert.Equal(expanded, planning.HasFullCustomerView);
        Assert.False(original.HasFullCustomerView); // CRM scope remains unchanged.
        var ownCompany = new SampleRequest { SampleRequestId = Guid.NewGuid(), CompanyId = user.CompanyId.Value,
            ManagerBy = Guid.NewGuid(), IsActive = true, CustomerId = Guid.NewGuid() };
        var foreign = new SampleRequest { SampleRequestId = Guid.NewGuid(), CompanyId = Guid.NewGuid(), IsActive = true };
        var customer = new Customer { CustomerId = ownCompany.CustomerId, CompanyId = user.CompanyId.Value, IsActive = true, IsLead = false };
        var visibility = new CustomerVisibilityService(null!, user, null!);
        var rows = visibility.ApplySampleRequestVisibility(new[] { ownCompany, foreign }.AsQueryable(), new[] { customer }.AsQueryable(), planning).ToArray();
        Assert.Equal(expanded ? 1 : 0, rows.Length);
        if (expanded) Assert.Equal(ownCompany.SampleRequestId, rows.Single().SampleRequestId);
        employee.IsActive = false;
        Assert.False((await SampleWorkPlanningVisibility.ForPlanningAsync(original, user, db.Employees, default)).HasFullCustomerView);
        Assert.False((await SampleWorkPlanningVisibility.ForPlanningAsync(original with { CompanyId = Guid.NewGuid() }, user, db.Employees, default)).HasFullCustomerView);
        Assert.Equal(0, store.Saves);
    }
}
