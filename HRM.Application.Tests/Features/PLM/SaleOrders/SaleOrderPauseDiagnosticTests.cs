using System.Reflection;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM.SaleOrders;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.SaleOrders.Commands.PauseSaleOrderDelivery;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.OrderSchema;

namespace HRM.Application.Tests.Features.PLM.SaleOrders;

public sealed class SaleOrderPauseDiagnosticTests
{
    [Theory]
    [InlineData("SaleUser", "assigned", true)]
    [InlineData("SaleUser", "manager-only", false)]
    [InlineData("SaleUser", "current-sale-only", false)]
    [InlineData("SaleUser", "inactive-assignment", false)]
    [InlineData("SaleUser", "completed", false)]
    [InlineData("SaleAdmin", "assigned", true)]
    [InlineData("ACUser", "assigned", true)]
    [InlineData("SaleUser", "read-visible", true)]
    [InlineData("User", "assigned", false)]
    [InlineData("SaleUser", "payment-resume", false)]
    [InlineData("SaleAdmin", "payment-resume", false)]
    [InlineData("ACUser", "payment-resume", false)]
    [InlineData("Developer", "manager-only", true)]
    public async Task Pause_ExercisesRealHandlerAndCustomerVisibility(string role, string scenario, bool expected)
    {
        var user = new User(role);
        var (db, store) = ProductionOrderTestStore.CreateContext<ISaleOrderDbContext>();
        var customer = new Customer { CustomerId = Guid.NewGuid(), CompanyId = user.CompanyId!.Value, IsActive = true, IsLead = false };
        if (scenario is "assigned" or "inactive-assignment" or "completed" or "payment-resume")
            customer.CustomerAssignments.Add(new CustomerAssignment
            { EmployeeId = user.EmployeeId!.Value, IsActive = scenario != "inactive-assignment" });
        if (scenario == "current-sale-only") customer.CurrentSaleId = user.EmployeeId;
        var order = new MerchandiseOrder
        {
            MerchandiseOrderId = Guid.NewGuid(), CustomerId = customer.CustomerId,
            CompanyId = user.CompanyId.Value, IsActive = true, ManagerById = user.EmployeeId!.Value,
            Status = scenario == "completed" ? "Completed" : "Approved"
        };
        store.Seed(nameof(db.Customers), customer);
        if (scenario == "payment-resume")
        {
            order.IsDeliveryPaused = true;
            order.DeliveryPauseType = "PaymentHold";
        }
        store.Seed(nameof(db.MerchandiseOrders), order);
        var visibility = DispatchProxy.Create<ICustomerVisibilityService, Visibility>();
        ((Visibility)(object)visibility).Scope = new ViewerScope(user.CompanyId.Value, user.EmployeeId.Value,
            scenario == "read-visible", false, new HashSet<Guid>(), new HashSet<Guid> { user.EmployeeId.Value }, new Clock().Now);
        var result = await new PauseSaleOrderDeliveryCommandHandler(db, user, new Clock(), null!, visibility)
            .Handle(new(new() { MerchandiseOrderId = order.MerchandiseOrderId, IsDeliveryPaused = scenario != "payment-resume",
                DeliveryPauseType = "Test", DeliveryPauseReason = "Diagnostic test" }), default);
        Assert.Equal(expected, result.Success);
        Assert.Equal(expected || scenario == "payment-resume", order.IsDeliveryPaused);
        Assert.Equal(expected ? 1 : 0, store.Saves);
        if (!expected)
            Assert.Equal(role == "User" ? "Bạn không có quyền tạm dừng giao hàng."
                : scenario == "payment-resume" ? "Đơn đang tạm dừng do công nợ. Sale không có quyền tiếp tục giao hàng."
                : scenario == "completed" ? "Đơn đã hủy hoặc giao xong, không thể tạm dừng/mở lại."
                : "Không tìm thấy đơn hàng.", result.Message);
    }

    public class Visibility : DispatchProxy
    {
        public ViewerScope Scope { get; set; } = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch
        {
            "BuildReadScopeAsync" => Task.FromResult(Scope),
            "ApplyMerchandiseOrderVisibility" => new CustomerVisibilityService(null!, null!, null!)
                .ApplyMerchandiseOrderVisibility((IQueryable<MerchandiseOrder>)args![0]!, (IQueryable<Customer>)args[1]!, Scope),
            _ => throw new NotSupportedException(method.Name)
        };
    }
    private sealed class Clock : IDateTimeProvider { public DateTime Now => new(2026, 11, 3); }
    private sealed class User(string role) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid? EmployeeId { get; } = Guid.NewGuid();
        public Guid? CompanyId { get; } = Guid.NewGuid();
        public string? UserName => "test";
        public string? Email => null;
        public IReadOnlyCollection<string> Roles => [role];
        public bool IsInRole(string value) => value == role;
    }
}
