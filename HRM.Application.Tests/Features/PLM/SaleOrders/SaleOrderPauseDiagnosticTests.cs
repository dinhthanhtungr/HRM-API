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
    [InlineData(false)]
    [InlineData(true)]
    public async Task NotificationKeepsActualStateBeforePatchAndActor(bool previous)
    {
        var user = new User("President");
        var (db, store) = ProductionOrderTestStore.CreateContext<ISaleOrderDbContext>();
        var order = new MerchandiseOrder {
            MerchandiseOrderId = Guid.NewGuid(), CompanyId = user.CompanyId!.Value,
            IsActive = true, ManagerById = Guid.NewGuid(), Status = "Approved", IsDeliveryPaused = previous,
            ExternalId = "DHG_TEST", DeliveryPauseType = "Test"
        };
        store.Seed(nameof(db.MerchandiseOrders), order);
        var notifications = DispatchProxy.Create<HRM.Application.Features.Notifications.Services.INotificationService, NotificationCapture>();
        var visibility = DispatchProxy.Create<ICustomerVisibilityService, Visibility>();
        ((Visibility)(object)visibility).Scope = new ViewerScope(user.CompanyId.Value, user.EmployeeId!.Value,
            true, false, new HashSet<Guid>(), new HashSet<Guid>(), new Clock().Now);
        var result = await new PauseSaleOrderDeliveryCommandHandler(db, user, new Clock(), notifications, visibility)
            .Handle(new(new() { MerchandiseOrderId = order.MerchandiseOrderId, IsDeliveryPaused = !previous,
                DeliveryPauseType = "Test", DeliveryPauseReason = "Recorded reason" }), default);
        Assert.True(result.Success);
        var request = Assert.Single(((NotificationCapture)(object)notifications).Requests);
        using var payload = System.Text.Json.JsonDocument.Parse(request.PayloadJson!);
        Assert.Equal(previous, payload.RootElement.GetProperty("previousIsDeliveryPaused").GetBoolean());
        Assert.Equal(!previous, payload.RootElement.GetProperty("isDeliveryPaused").GetBoolean());
        Assert.Equal("Recorded reason", payload.RootElement.GetProperty("deliveryPauseReason").GetString());
        Assert.Equal(user.UserName, request.CreatedByNameSnapshot);
    }

    public class NotificationCapture : DispatchProxy
    {
        public List<HRM.Application.Features.Notifications.Dtos.PublishNotificationRequest> Requests { get; } = [];
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            Requests.Add((HRM.Application.Features.Notifications.Dtos.PublishNotificationRequest)args![0]!);
            return Task.FromResult(Guid.NewGuid());
        }
    }

    [Theory]
    [InlineData("SaleUser", "assigned", true)]
    [InlineData("SaleUser", "manager-only", false)]
    [InlineData("SaleUser", "current-sale-only", false)]
    [InlineData("SaleUser", "inactive-assignment", false)]
    [InlineData("SaleUser", "completed", false)]
    [InlineData("SaleAdmin", "assigned", true)]
    [InlineData("ACUser", "assigned", true)]
    [InlineData("SaleUser", "read-visible", true)]
    [InlineData("User", "assigned", true)]
    [InlineData("PLPUUser", "read-visible", true)]
    [InlineData("LabAdmin", "read-visible", true)]
    [InlineData("User", "manager-only", false)]
    [InlineData("Developer", "manager-only", false)]
    [InlineData("User", "foreign-company", false)]
    [InlineData("User", "cancelled", false)]
    [InlineData("User", "delivered", false)]
    [InlineData("SaleUser", "payment-resume", false)]
    [InlineData("SaleAdmin", "payment-resume", false)]
    [InlineData("ACUser", "payment-resume", false)]
    [InlineData("SaleUser", "payment-retype", false)]
    [InlineData("SaleAdmin", "payment-retype", false)]
    [InlineData("ACUser", "payment-retype", false)]
    [InlineData("Developer", "payment-resume", true)]
    [InlineData("SaleUser", "payment-dates", false)]
    [InlineData("SaleUser", "inactive-order", false)]
    [InlineData("User", "canceled", false)]
    public async Task Pause_ExercisesRealHandlerAndCustomerVisibility(string role, string scenario, bool expected)
    {
        var user = new User(role);
        var (db, store) = ProductionOrderTestStore.CreateContext<ISaleOrderDbContext>();
        var customer = new Customer { CustomerId = Guid.NewGuid(), CompanyId = user.CompanyId!.Value, IsActive = true, IsLead = false };
        if (scenario is "assigned" or "inactive-assignment" or "completed" or "payment-resume" or "payment-retype" or "cancelled" or "delivered" or "foreign-company")
            customer.CustomerAssignments.Add(new CustomerAssignment
            { EmployeeId = user.EmployeeId!.Value, IsActive = scenario != "inactive-assignment" });
        if (scenario == "current-sale-only") customer.CurrentSaleId = user.EmployeeId;
        var order = new MerchandiseOrder
        {
            MerchandiseOrderId = Guid.NewGuid(), CustomerId = customer.CustomerId,
            CompanyId = scenario == "foreign-company" ? Guid.NewGuid() : user.CompanyId.Value, IsActive = scenario != "inactive-order", ManagerById = user.EmployeeId!.Value,
            Status = scenario switch { "completed" => "Completed", "cancelled" => "Cancelled", "canceled" => "Canceled", "delivered" => "Delivered", _ => "Approved" }
        };
        store.Seed(nameof(db.Customers), customer);
        if (scenario is "payment-resume" or "payment-retype" or "payment-dates")
        {
            order.IsDeliveryPaused = true;
            order.DeliveryPauseType = "PaymentHold";
        }
        store.Seed(nameof(db.MerchandiseOrders), order);
        var visibility = DispatchProxy.Create<ICustomerVisibilityService, Visibility>();
        ((Visibility)(object)visibility).Scope = new ViewerScope(user.CompanyId.Value, user.EmployeeId.Value,
            scenario is "read-visible" or "payment-dates" or "canceled", false, new HashSet<Guid>(), new HashSet<Guid> { user.EmployeeId.Value }, new Clock().Now);
        var result = await new PauseSaleOrderDeliveryCommandHandler(db, user, new Clock(), null!, visibility)
            .Handle(new(new() { MerchandiseOrderId = order.MerchandiseOrderId, IsDeliveryPaused = scenario != "payment-resume",
                DeliveryPauseType = scenario == "payment-dates" ? "PaymentHold" : "Test",
                DeliveryPausedTo = scenario == "payment-dates" ? new Clock().Now.AddDays(-1) : null,
                DeliveryPauseReason = "Diagnostic test" }), default);
        Assert.Equal(expected, result.Success);
        Assert.Equal(scenario == "payment-resume" ? !expected : expected || scenario is "payment-retype" or "payment-dates", order.IsDeliveryPaused);
        Assert.Equal(expected ? 1 : 0, store.Saves);
        if (!expected && scenario is "payment-resume" or "payment-retype" or "payment-dates")
            Assert.Equal("PaymentHold", order.DeliveryPauseType);
        if (!expected)
            Assert.Equal(scenario is "payment-resume" or "payment-retype" or "payment-dates" ? "Đơn đang tạm dừng do công nợ. Sale không có quyền tiếp tục giao hàng."
                : scenario is "completed" or "cancelled" or "canceled" or "delivered" ? "Đơn đã hủy hoặc giao xong, không thể tạm dừng/mở lại."
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
