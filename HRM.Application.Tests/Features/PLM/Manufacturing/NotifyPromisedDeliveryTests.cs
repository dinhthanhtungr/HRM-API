using System.Reflection;
using System.Text.Json;
using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.CRM.CustomerCare.Services;
using HRM.Application.Features.Notifications.Dtos;
using HRM.Application.Features.Notifications.Services;
using HRM.Application.Features.PLM.Manufacturing.Commands.NotifyPromisedDelivery;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Enums.Notifications;

namespace HRM.Application.Tests.Features.PLM.Manufacturing;

public sealed class NotifyPromisedDeliveryTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(15)]
    public async Task SameCalendarDate_DoesNotPublishOrResolveRecipients(int hour)
    {
        var f = new Fixture();
        f.Order.ExpectedDate = new DateTime(2026, 10, 20, hour, 30, 0);
        var before = f.Order.ExpectedDate;
        var result = await f.Handler.Handle(f.Request, default);
        Assert.True(result.Success);
        Assert.Equal(Guid.Empty, result.Data);
        Assert.Empty(f.Capture.Published);
        Assert.Empty(f.GroupCapture.Sales);
        Assert.Equal(before, f.Order.ExpectedDate);
        Assert.Equal(0, f.Store.Saves);
        Assert.Empty(f.Store.Added);
    }

    [Fact]
    public async Task EarlierDate_StillPublishes()
    {
        var f = new Fixture();
        var result = await f.Handler.Handle(f.Request with { PromisedDeliveryDate = new(2026, 10, 18) }, default);
        Assert.True(result.Success);
        Assert.Single(f.Capture.Published);
        Assert.Equal(0, f.Store.Saves);
    }

    [Fact]
    public async Task SendsToAllLinkedSalesAndTheirLeaders_WithoutUpdatingOrders()
    {
        var f = new Fixture();
        var sale = Guid.NewGuid();
        f.Store.Seed(nameof(f.Db.Employees), f.Employee(f.Sale), f.Employee(sale));
        f.Store.Seed(nameof(f.Db.MfgOrderPOs), f.Link(f.Sale), f.Link(f.Sale), f.Link(sale), f.Link(Guid.NewGuid(), false), f.Link(Guid.NewGuid(), foreign: true));
        var before = f.Order.ExpectedDate;
        var result = await f.Handler.Handle(f.Request, default);
        Assert.True(result.Success);
        Assert.Equal(f.Capture.NotificationId, result.Data);
        var notification = Assert.Single(f.Capture.Published);
        Assert.True(new[] { f.Sale, sale, f.Leader }.ToHashSet().SetEquals(notification.TargetUserIds!));
        Assert.Equal(2, f.GroupCapture.Sales.Count);
        Assert.Equal(f.Order.CompanyId, notification.CompanyId);
        Assert.Contains(f.Db.MfgOrderPOs.Where(l => l.IsActive && l.Detail.MerchandiseOrder.CompanyId == f.User.CompanyId).Select(l => l.Detail.MerchandiseOrder.MerchandiseOrderId), id => id == notification.AggregateId);
        Assert.Equal(TopicNotifications.MerchandiseOrderPromisedDeliveryNotified, notification.Topic);
        Assert.Equal("DHG_TEST", notification.Title);
        Assert.Contains("Ngày hứa giao đổi từ 19/10/2026 thành 20/10/2026.", notification.Message);
        Assert.Equal("DHG_TEST", notification.AggregateCode);
        Assert.Equal("/plm/sale-orders?keyword=DHG_TEST", notification.Link);
        Assert.Null(notification.TargetRoles);
        using var payload = JsonDocument.Parse(notification.PayloadJson!);
        Assert.Equal("2026-10-20", payload.RootElement.GetProperty("promisedDeliveryDate").GetString());
        Assert.Equal("2026-10-19", payload.RootElement.GetProperty("previousPromisedDeliveryDate").GetString());
        Assert.Equal("MFG_TEST", payload.RootElement.GetProperty("manufacturingOrder").GetProperty("externalId").GetString());
        Assert.Equal(before, f.Order.ExpectedDate);
        Assert.Equal(0, f.Store.Saves);
        Assert.Empty(f.Store.Added);
    }

    [Fact]
    public async Task MissingPreviousDateIsExplicitAndDoesNotUpdateOrder()
    {
        var f = new Fixture();
        f.Order.ExpectedDate = null;
        Assert.True((await f.Handler.Handle(f.Request, default)).Success);
        var notification = Assert.Single(f.Capture.Published);
        Assert.Contains("Ngày hứa giao đổi từ chưa xác định thành 20/10/2026.", notification.Message);
        using var payload = JsonDocument.Parse(notification.PayloadJson!);
        Assert.Equal(JsonValueKind.Null, payload.RootElement.GetProperty("previousPromisedDeliveryDate").ValueKind);
        Assert.Null(f.Order.ExpectedDate);
        Assert.Equal(0, f.Store.Saves);
    }

    [Theory]
    [InlineData("permission")]
    [InlineData("company")]
    [InlineData("inactive")]
    [InlineData("duplicate")]
    [InlineData("no-links")]
    [InlineData("inactive-detail")]
    [InlineData("inactive-sale")]
    [InlineData("no-date")]
    [InlineData("empty-code")]
    public async Task InvalidScopeAndInputNeverPublish(string reason)
    {
        var f = new Fixture();
        if (reason == "permission") f.Permissions.Allowed = false;
        if (reason == "company") f.Order.CompanyId = Guid.NewGuid();
        if (reason == "inactive") f.Order.IsActive = false;
        if (reason == "duplicate") f.Store.Seed(nameof(f.Db.MfgProductionOrders), f.Order,
            new MfgProductionOrder { MfgProductionOrderId = Guid.NewGuid(), CompanyId = f.User.CompanyId!.Value, ExternalId = f.Order.ExternalId, IsActive = true });
        if (reason == "no-links") f.Store.Seed<MfgOrderPO>(nameof(f.Db.MfgOrderPOs));
        if (reason == "inactive-detail") f.Db.MfgOrderPOs.Single().Detail.IsActive = false;
        if (reason == "inactive-sale") f.Db.Employees.Single().IsActive = false;
        var request = reason == "no-date" ? f.Request with { PromisedDeliveryDate = default }
            : reason == "empty-code" ? f.Request with { ExternalId = " " } : f.Request;
        Assert.False((await f.Handler.Handle(request, default)).Success);
        Assert.Empty(f.Capture.Published);
        Assert.Equal(0, f.Store.Saves);
    }

    [Fact]
    public void NewTopicIsConfiguredWithoutChangingLegacyTopic()
    {
        var topic = NotificationTopicCatalog.GetDefinition(TopicNotifications.MerchandiseOrderPromisedDeliveryNotified);
        Assert.Equal("sales.merchandise_order.promised_delivery_notified", topic.Code);
        Assert.Equal("sales_order", topic.CategoryCode);
        Assert.Equal("MerchandiseOrder", topic.AggregateType);
        Assert.Equal(55, (int)TopicNotifications.MerchandiseOrderPromisedDeliveryNotified);
        Assert.Equal("schedule", topic.EventGroupCode);
        Assert.Equal("mfg.production_order.expected_date_changed", NotificationTopicCatalog.GetDefinition(TopicNotifications.MfgProductionOrderChangeExpectiveDate).Code);
    }

    private sealed class Fixture
    {
        public IManufacturingDbContext Db { get; }
        public ProductionOrderTestStore Store { get; }
        public User User { get; } = new();
        public Permissions Permissions { get; } = new();
        public Guid Sale { get; } = Guid.NewGuid();
        public Guid Leader { get; } = Guid.NewGuid();
        public MfgProductionOrder Order { get; }
        public CapturePort Capture { get; }
        public CapturePort GroupCapture { get; }
        public NotifyPromisedDeliveryCommandHandler Handler { get; }
        public NotifyPromisedDeliveryCommand Request => new() { ExternalId = " MFG_TEST ", PromisedDeliveryDate = new(2026, 10, 20) };
        public Fixture()
        {
            (Db, Store) = ProductionOrderTestStore.CreateContext<IManufacturingDbContext>();
            Order = new() { MfgProductionOrderId = Guid.NewGuid(), CompanyId = User.CompanyId!.Value, ExternalId = "MFG_TEST", IsActive = true, ExpectedDate = new(2026, 10, 19) };
            Store.Seed(nameof(Db.MfgProductionOrders), Order);
            Store.Seed(nameof(Db.Employees), Employee(Sale));
            Store.Seed(nameof(Db.MfgOrderPOs), Link(Sale));
            var notifications = DispatchProxy.Create<INotificationService, CapturePort>();
            Capture = (CapturePort)(object)notifications;
            var groups = DispatchProxy.Create<ISaleGroupRecipientResolver, CapturePort>();
            GroupCapture = (CapturePort)(object)groups;
            GroupCapture.Leader = Leader;
            Handler = new(Db, User, Permissions, groups, notifications);
        }
        public Employee Employee(Guid id) => new() { EmployeeId = id, CompanyId = User.CompanyId!.Value, IsActive = true };
        public MfgOrderPO Link(Guid sale, bool active = true, bool foreign = false) => new()
        {
            MfgProductionOrderId = Order.MfgProductionOrderId, IsActive = active,
            Detail = new() { IsActive = true, MerchandiseOrder = new MerchandiseOrder
            {
                MerchandiseOrderId = Guid.NewGuid(), ExternalId = "DHG_TEST", IsActive = true,
                CompanyId = foreign ? Guid.NewGuid() : User.CompanyId!.Value, ManagerById = sale
            } }
        };
    }

    public class CapturePort : DispatchProxy
    {
        public Guid NotificationId { get; } = Guid.NewGuid();
        public Guid Leader { get; set; }
        public List<Guid> Sales { get; } = [];
        public List<PublishNotificationRequest> Published { get; } = [];
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == nameof(INotificationService.PublishAsync))
            { Published.Add((PublishNotificationRequest)args![0]!); return Task.FromResult(NotificationId); }
            if (method.Name == nameof(ISaleGroupRecipientResolver.ResolveSaleGroupLeaderIdsAsync))
            { Sales.Add((Guid)args![1]!); return Task.FromResult<IReadOnlyCollection<Guid>>([Leader]); }
            throw new NotSupportedException(method.Name);
        }
    }
    private sealed class Permissions : ICurrentUserPermissionService
    {
        public bool Allowed { get; set; } = true;
        public bool HasPermission(string permission) => Allowed && permission == ApplicationPermissions.PLM.ManageProductionOrders;
    }
    private sealed class User : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid? EmployeeId { get; } = Guid.NewGuid();
        public Guid? CompanyId { get; } = Guid.NewGuid();
        public string? UserName => null;
        public string? Email => null;
        public IReadOnlyCollection<string> Roles => [];
        public bool IsInRole(string role) => false;
    }
}
