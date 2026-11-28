using System.Reflection;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM.DeliverySchedules;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.SaleOrders.DeliverySchedules;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Entities.DeliverySchema;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

namespace HRM.Application.Tests.Features.PLM.SaleOrders;

public sealed partial class DeliveryScheduleServiceTests
{
    [Theory]
    [InlineData(null, "2026-12-01")]
    [InlineData("2026-12-01", "2026-11-29")]
    [InlineData("2026-11-29", null)]
    public async Task ChangedSchedulePromiseQueuesOnlyResponsibleSaleWithOldNewDatesAndQuantity(string? before, string? after)
    {
        var f = new Fixture(true);
        f.A.PromisedDate = before is null ? null : DateTime.Parse(before);
        var current = (await f.Service.GetAsync(f.Order.MerchandiseOrderId, f.Detail.MerchandiseOrderDetailId, default))!;
        var next = new[] { f.Input(f.A) with { PromisedDate = after is null ? null : DateOnly.Parse(after) }, f.Input(f.B) };
        var result = await f.Service.SaveAsync(f.Order.MerchandiseOrderId, f.Detail.MerchandiseOrderDetailId, new(current.Revision, next, []), default);
        Assert.True(result.Success, result.Message);
        Assert.True(f.Store.Transaction.Committed);
        var notification = Assert.Single(f.Notifications!.Published);
        Assert.Equal(new[] { f.Order.ManagerById }, notification.TargetUserIds);
        Assert.True(notification.UseExactAudience);
        Assert.Equal(HRM.Domain.Enums.Notifications.TopicNotifications.MerchandiseOrderPromisedDeliveryNotified, notification.Topic);
        Assert.Contains("400 kg", notification.Message);
        Assert.Contains("→", notification.Message);
        using var json = System.Text.Json.JsonDocument.Parse(notification.PayloadJson!);
        Assert.Equal(before, json.RootElement.GetProperty("previousPromisedDeliveryDate").GetString());
        Assert.Equal(after, json.RootElement.GetProperty("promisedDeliveryDate").GetString());
        Assert.Equal(400, json.RootElement.GetProperty("deliverySchedule").GetProperty("quantity").GetDecimal());
        var latest = (await f.Service.GetAsync(f.Order.MerchandiseOrderId, f.Detail.MerchandiseOrderDetailId, default))!;
        Assert.True((await f.Service.SaveAsync(f.Order.MerchandiseOrderId, f.Detail.MerchandiseOrderDetailId, new(latest.Revision, [f.Input(f.A), f.Input(f.B)], []), default)).Success);
        Assert.Single(f.Notifications.Published);
    }

    [Theory]
    [InlineData("unchanged")]
    [InlineData("quantity-only")]
    [InlineData("stale")]
    [InlineData("closed")]
    [InlineData("foreign-sale")]
    [InlineData("inactive-sale")]
    [InlineData("legacy-one-date")]
    public async Task DoesNotQueuePromiseNotificationsWithoutACommittedEligibleChange(string reason)
    {
        var f = new Fixture(true);
        if (reason == "legacy-one-date") f.B.IsActive = false;
        if (reason is "foreign-sale" or "inactive-sale")
            f.Store.Seed(nameof(f.Db.Employees), new HRM.Domain.Entities.HrSchema.Employee { EmployeeId = f.Order.ManagerById, CompanyId = reason == "foreign-sale" ? Guid.NewGuid() : f.Company, IsActive = reason != "inactive-sale" });
        if (reason == "closed") f.Order.Status = "Delivered";
        var current = (await f.Service.GetAsync(f.Order.MerchandiseOrderId, f.Detail.MerchandiseOrderDetailId, default))!;
        var next = new[] { f.Input(f.A) with { PromisedDate = reason is "unchanged" or "quantity-only" ? null : new DateOnly(2026, 12, 1), Quantity = reason == "quantity-only" ? 450 : 400 },
            f.Input(f.B) with { Quantity = reason == "quantity-only" ? 550 : 600 } };
        await f.Service.SaveAsync(f.Order.MerchandiseOrderId, f.Detail.MerchandiseOrderDetailId,
            new(reason == "stale" ? "stale" : current.Revision, reason == "legacy-one-date" ? [next[0] with { Quantity = 1000 }] : next, []), default);
        Assert.Empty(f.Notifications!.Published);
    }

    [Fact]
    public async Task DatabaseFailureQueuesNoNotification()
    {
        var f = new Fixture(true);
        var current = (await f.Service.GetAsync(f.Order.MerchandiseOrderId, f.Detail.MerchandiseOrderDetailId, default))!;
        f.Store.ThrowOnSave = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.SaveAsync(f.Order.MerchandiseOrderId, f.Detail.MerchandiseOrderDetailId,
            new(current.Revision, [f.Input(f.A) with { PromisedDate = new DateOnly(2026, 12, 1) }, f.Input(f.B)], []), default));
        Assert.False(f.Store.Transaction.Committed);
        Assert.True(f.Store.Transaction.RolledBack);
        Assert.Empty(f.Notifications!.Published);
    }
    [Fact]
    public async Task DisabledUiDoesNotTouchNewTables()
    {
        var user = new ProductionOrderCreationTests.TestUser();
        var options = Options.Create(new DeliveryScheduleOptions());
        var service = new DeliveryScheduleService(null!, user, new CurrentUserPermissionService(user), null!, new Clock(), options,
            new(null!, options));
        Assert.False((await service.GetAsync(Guid.NewGuid(), Guid.NewGuid(), default))!.Enabled);
        Assert.True((await new DeliveryScheduleFulfillmentService(null!, options).AllocateAsync(new(), [], Guid.NewGuid(), Guid.NewGuid(), Clock.NowValue, default)).Success);
    }

    [Fact]
    public void NewOrderCannotBypassRequestedDateValidationThroughNestedSchedules()
    {
        var f = new Fixture();
        var error = f.Service.PrepareNewDetail(f.Detail, [new(null, 1000, DateOnly.FromDateTime(Clock.NowValue.AddDays(-1)), null, null, null)], f.User.EmployeeId!.Value, Clock.NowValue);
        Assert.NotNull(error); Assert.Empty(f.Detail.DeliverySchedules); Assert.Equal(0, f.Store.Saves);
    }

    [Fact]
    public void NewOrderRequestedDateUsesBusinessDayInsteadOfUtcDay()
    {
        var f = new Fixture();
        var now = new DateTime(2026, 11, 18, 18, 0, 0, DateTimeKind.Utc);
        Assert.NotNull(f.Service.PrepareNewDetail(f.Detail, [new(null, 1000, new DateOnly(2026, 11, 18), null, null, null)], f.User.EmployeeId!.Value, now));
        Assert.Empty(f.Detail.DeliverySchedules);
    }

    [Theory]
    [InlineData("Developer", true, true)]
    [InlineData("SaleUser", true, false)]
    [InlineData("PLPUUser", false, true)]
    [InlineData("DispatchUser", false, true)]
    [InlineData("LabUser", false, false)]
    public async Task WorkspaceSeparatesRequestAndPromiseCapabilities(string role, bool request, bool promise)
    {
        var f = new Fixture(); f.User.Roles = [role];
        var data = await f.Service.GetAsync(f.Order.MerchandiseOrderId, f.Detail.MerchandiseOrderDetailId, default);
        Assert.NotNull(data); Assert.Equal(request, data!.CanEditRequest); Assert.Equal(promise, data.CanEditPromise);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RejectsForeignCompanyAndOutOfScopeCustomer(bool company)
    {
        var f = new Fixture(); f.User.Roles = ["SaleUser"];
        if (company) f.Order.CompanyId = Guid.NewGuid(); else f.Scope.CustomerId = Guid.NewGuid();
        Assert.Null(await f.Service.GetAsync(f.Order.MerchandiseOrderId, f.Detail.MerchandiseOrderDetailId, default));
    }

    [Fact]
    public async Task StaleRevisionDoesNotSaveOrCommit()
    {
        var f = new Fixture();
        var result = await f.Service.SaveAsync(f.Order.MerchandiseOrderId, f.Detail.MerchandiseOrderDetailId,
            new("stale", [f.Input(f.A)], []), default);
        Assert.False(result.Success); Assert.Equal(0, f.Store.Saves); Assert.False(f.Store.Transaction.Committed);
    }

    [Theory]
    [InlineData("anonymous")]
    [InlineData("explicit-no-permission")]
    [InlineData("closed")]
    public async Task MutationCannotBypassAuthenticationExplicitCapabilityOrClosedOrder(string scenario)
    {
        var f = new Fixture();
        if (scenario == "anonymous") f.User.IsAuthenticated = false;
        if (scenario == "explicit-no-permission") f.User.HasExplicitPermissionSet = true;
        if (scenario == "closed") f.Order.Status = "Cancelled";
        var result = await f.Service.SaveAsync(f.Order.MerchandiseOrderId, f.Detail.MerchandiseOrderDetailId,
            new("r", [f.Input(f.A), f.Input(f.B)], []), default);
        Assert.False(result.Success); Assert.Equal(0, f.Store.Saves); Assert.Empty(f.Store.Added);
    }

    [Fact]
    public async Task NullScheduleArrayElementIsRejectedInsteadOfThrowing()
    {
        var f = new Fixture();
        var result = await f.Service.SaveAsync(f.Order.MerchandiseOrderId, f.Detail.MerchandiseOrderDetailId, new("r", [null!], []), default);
        Assert.False(result.Success); Assert.Equal(0, f.Store.Saves);
        Assert.Throws<ArgumentException>(() => DeliveryScheduleAllocationRules.ValidateManual(100, [], [null!]));
    }

    [Fact]
    public async Task ReadAllCustomerRoleDoesNotGrantScheduleMutationOutsideOwnership()
    {
        var f = new Fixture(); f.User.Roles = ["SaleUser", "CustomerViewAll"];
        f.Scope.CustomerId = Guid.NewGuid(); f.Scope.HonorFullView = true;
        f.Scope.Scope = f.Scope.Scope with { HasFullCustomerView = true };
        var data = await f.Service.GetAsync(f.Order.MerchandiseOrderId, f.Detail.MerchandiseOrderDetailId, default);
        Assert.NotNull(data); Assert.False(data!.CanEditRequest);
        var result = await f.Service.SaveAsync(f.Order.MerchandiseOrderId, f.Detail.MerchandiseOrderDetailId,
            new(data.Revision, [f.Input(f.A), f.Input(f.B)], []), default);
        Assert.False(result.Success); Assert.Equal(0, f.Store.Saves);
    }

    [Fact]
    public async Task SaveUpdatesDatesWithoutChangingOrderedQuantityOrMoney()
    {
        var f = new Fixture();
        var current = (await f.Service.GetAsync(f.Order.MerchandiseOrderId, f.Detail.MerchandiseOrderDetailId, default))!;
        var next = current.Items.Select(x => f.Input(x.Id == f.A.MerchandiseOrderDeliveryScheduleId ? f.A : f.B) with { PromisedDate = new DateOnly(2026, 12, 1) }).ToArray();
        var result = await f.Service.SaveAsync(f.Order.MerchandiseOrderId, f.Detail.MerchandiseOrderDetailId, new(current.Revision, next, []), default);
        Assert.True(result.Success, result.Message); Assert.True(f.Store.Transaction.Committed);
        Assert.Equal(1000, f.Detail.ExpectedQuantity); Assert.Equal(12345, f.Detail.UnitPriceAgreed);
        Assert.Equal(1, f.Store.Saves); Assert.Single(f.Store.Added.OfType<HRM.Domain.Entities.AuditSchema.AuditLog>());
    }

    [Theory]
    [InlineData("PLPUUser", true)]
    [InlineData("SaleUser", false)]
    public async Task PromiseOnlySaveUsesExistingRoleCapability(string role, bool allowed)
    {
        var f = new Fixture();
        f.User.Roles = [role];
        var current = (await f.Service.GetAsync(f.Order.MerchandiseOrderId, f.Detail.MerchandiseOrderDetailId, default))!;
        var requestedA = f.A.RequestedDate;
        var quantityA = f.A.Quantity;
        var next = new[] { f.Input(f.A) with { PromisedDate = new DateOnly(2026, 12, 1) }, f.Input(f.B) };
        var result = await f.Service.SaveAsync(f.Order.MerchandiseOrderId, f.Detail.MerchandiseOrderDetailId,
            new(current.Revision, next, []), default);
        Assert.Equal(allowed, result.Success);
        Assert.Equal(allowed, f.Store.Transaction.Committed);
        Assert.Equal(requestedA, f.A.RequestedDate);
        Assert.Equal(quantityA, f.A.Quantity);
        Assert.Equal(allowed ? 1 : 0, f.Store.Saves);
        if (allowed) Assert.Equal(new DateTime(2026, 12, 1), f.A.PromisedDate);
    }

    [Fact]
    public async Task OrderClosedDuringSaveDoesNotCommitSchedules()
    {
        var f = new Fixture();
        var current = (await f.Service.GetAsync(f.Order.MerchandiseOrderId, f.Detail.MerchandiseOrderDetailId, default))!;
        f.Store.BeforeSave = () => f.Order.Status = "Delivered";
        var next = new[] { f.Input(f.A) with { PromisedDate = new DateOnly(2026, 12, 1) }, f.Input(f.B) };
        var result = await f.Service.SaveAsync(f.Order.MerchandiseOrderId, f.Detail.MerchandiseOrderDetailId, new(current.Revision, next, []), default);
        Assert.False(result.Success);
        Assert.False(f.Store.Transaction.Committed);
        Assert.True(f.Store.Transaction.RolledBack);
    }

    [Fact]
    public async Task SavingUnchangedScheduleDoesNotCreateAnotherAudit()
    {
        var f = new Fixture();
        var data = (await f.Service.GetAsync(f.Order.MerchandiseOrderId, f.Detail.MerchandiseOrderDetailId, default))!;
        var result = await f.Service.SaveAsync(f.Order.MerchandiseOrderId, f.Detail.MerchandiseOrderDetailId,
            new(data.Revision, [f.Input(f.A), f.Input(f.B)], []), default);
        Assert.True(result.Success); Assert.Equal(0, f.Store.Saves); Assert.Empty(f.Store.Added);
    }

    [Fact]
    public async Task PromiseEditorCanBootstrapOneDateWithoutChangingRequestOrMoney()
    {
        var f = new Fixture(); f.User.Roles = ["PLPUUser"];
        f.Store.Seed<MerchandiseOrderDeliverySchedule>(nameof(f.Db.MerchandiseOrderDeliverySchedules));
        f.Detail.ExpectedDeliveryDate = Clock.NowValue.AddDays(1);
        var data = (await f.Service.GetAsync(f.Order.MerchandiseOrderId, f.Detail.MerchandiseOrderDetailId, default))!;
        var input = new DeliveryScheduleInput(null, 1000, DateOnly.FromDateTime(Clock.NowValue), new DateOnly(2026, 11, 25), null, null);
        var result = await f.Service.SaveAsync(f.Order.MerchandiseOrderId, f.Detail.MerchandiseOrderDetailId, new(data.Revision, [input], []), default);
        Assert.True(result.Success, result.Message); Assert.Single(f.Store.Added.OfType<MerchandiseOrderDeliverySchedule>());
        Assert.Equal(12345, f.Detail.UnitPriceAgreed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RequestEditorMayPreserveButCannotReplaceLegacyPromiseWhenBootstrapping(bool replacePromise)
    {
        var f = new Fixture(); f.User.Roles = ["SaleUser"];
        f.Store.Seed<MerchandiseOrderDeliverySchedule>(nameof(f.Db.MerchandiseOrderDeliverySchedules));
        f.Detail.ExpectedDeliveryDate = Clock.NowValue.AddDays(1);
        var data = (await f.Service.GetAsync(f.Order.MerchandiseOrderId, f.Detail.MerchandiseOrderDetailId, default))!;
        var input = new DeliveryScheduleInput(null, 1000, DateOnly.FromDateTime(Clock.NowValue), data.LegacyPromisedDate!.Value.AddDays(replacePromise ? 1 : 0), null, null);
        var result = await f.Service.SaveAsync(f.Order.MerchandiseOrderId, f.Detail.MerchandiseOrderDetailId, new(data.Revision, [input], []), default);
        Assert.Equal(!replacePromise, result.Success);
        Assert.Equal(replacePromise ? 0 : 1, f.Store.Saves);
    }

    [Fact]
    public async Task HiddenUiStillAllocatesStorageAndOneLineCanSpanTwoSchedules()
    {
        var f = new Fixture(); f.Options.Value.EnableMultiScheduleUi = false;
        var order = new DeliveryOrder { Id = Guid.NewGuid(), CompanyId = f.Company, IsActive = true, Status = "Pending" };
        var row = new DeliveryOrderDetail { Id = Guid.NewGuid(), MerchandiseOrderDetailId = f.Detail.MerchandiseOrderDetailId, DeliveryOrderId = order.Id, Quantity = 500 };
        order.Details.Add(row);
        var result = await f.Fulfillment.AllocateAsync(order, [new() { MerchandiseOrderDetailId = f.Detail.MerchandiseOrderDetailId, Quantity = 500 }], f.Company, f.User.EmployeeId!.Value, Clock.NowValue, default);
        Assert.True(result.Success, result.Message);
        var parts = f.Store.Added.OfType<DeliveryOrderScheduleAllocation>().ToArray();
        Assert.Equal(2, parts.Length); Assert.Equal(500, parts.Sum(x => x.Quantity));
        Assert.All(parts, x => Assert.Equal(row.Id, x.DeliveryOrderDetailId)); Assert.Equal(0, f.Store.Saves);
    }

    [Fact]
    public async Task PendingDeliveryReservesCapacityAndEditingSameQuantityPreservesExistingSplit()
    {
        var f = new Fixture();
        var order = new DeliveryOrder { Id = Guid.NewGuid(), CompanyId = f.Company, IsActive = true, Status = "Pending" };
        var row = new DeliveryOrderDetail { Id = Guid.NewGuid(), MerchandiseOrderDetailId = f.Detail.MerchandiseOrderDetailId, DeliveryOrderId = order.Id,
            DeliveryOrder = order, Quantity = 100, IsActive = true };
        var allocation = new DeliveryOrderScheduleAllocation { DeliveryOrderDetailId = row.Id, DeliveryOrderDetail = row,
            MerchandiseOrderDeliveryScheduleId = f.B.MerchandiseOrderDeliveryScheduleId, Schedule = f.B, Quantity = 100 };
        f.Store.Seed(nameof(f.Db.DeliveryOrderDetails), row); f.Store.Seed(nameof(f.Db.DeliveryOrderScheduleAllocations), allocation);
        var workspace = (await f.Service.GetAsync(f.Order.MerchandiseOrderId, f.Detail.MerchandiseOrderDetailId, default))!;
        Assert.Equal(100, workspace.Items.Single(x => x.Id == f.B.MerchandiseOrderDeliveryScheduleId).AllocatedQuantity);
        var preview = await f.Service.PreviewAsync(f.Order.MerchandiseOrderId, f.Detail.MerchandiseOrderDetailId, new(100, order.Id), default);
        Assert.True(preview.Success, preview.Message);
        Assert.Equal(f.B.MerchandiseOrderDeliveryScheduleId, Assert.Single(preview.Data!.Allocations).ScheduleId);
    }

    [Fact]
    public async Task ExcessDeliveryIsExplicitlyRecordedOutsideSchedule()
    {
        var f = new Fixture();
        var order = new DeliveryOrder { Id = Guid.NewGuid(), CompanyId = f.Company, IsActive = true, Status = "Pending" };
        order.Details.Add(new DeliveryOrderDetail { Id = Guid.NewGuid(), MerchandiseOrderDetailId = f.Detail.MerchandiseOrderDetailId, DeliveryOrderId = order.Id, Quantity = 1200 });
        var result = await f.Fulfillment.AllocateAsync(order, [new() { MerchandiseOrderDetailId = f.Detail.MerchandiseOrderDetailId, Quantity = 1200 }], f.Company, f.User.EmployeeId!.Value, Clock.NowValue, default);
        Assert.True(result.Success);
        var allocations = f.Store.Added.OfType<DeliveryOrderScheduleAllocation>().ToArray();
        Assert.Equal(1200, allocations.Sum(x => x.Quantity));
        Assert.Equal(200, Assert.Single(allocations.Where(x => x.MerchandiseOrderDeliveryScheduleId is null)).Quantity);
    }

    [Fact]
    public async Task ReviewedOutsideScheduleRowIsNotMisclassifiedAsAnUnmappedLegacySlip()
    {
        var f = new Fixture();
        var order = new DeliveryOrder { Id = Guid.NewGuid(), CompanyId = f.Company, IsActive = true, Status = "Pending" };
        var row = new DeliveryOrderDetail { Id = Guid.NewGuid(), DeliveryOrderId = order.Id, DeliveryOrder = order,
            MerchandiseOrderDetailId = f.Detail.MerchandiseOrderDetailId, Quantity = 200 };
        f.Store.Seed(nameof(f.Db.DeliveryOrderDetails), row);
        f.Store.Seed(nameof(f.Db.DeliveryOrderScheduleAllocations), new DeliveryOrderScheduleAllocation { DeliveryOrderDetailId = row.Id, DeliveryOrderDetail = row,
            MerchandiseOrderDeliveryScheduleId = null, Quantity = 200 });
        var data = (await f.Service.GetAsync(f.Order.MerchandiseOrderId, f.Detail.MerchandiseOrderDetailId, default))!;
        Assert.False(data.HasUnreconciledDelivery); Assert.True(data.CanEditRequest);
    }

    private sealed class Fixture
    {
        public ProductionOrderCreationTests.TestUser User { get; } = new() { Roles = ["Developer"] };
        public Guid Company => User.CompanyId!.Value;
        public IDeliveryScheduleDbContext Db { get; }
        public Store Store { get; }
        public ScopeProxy Scope { get; }
        public MerchandiseOrder Order { get; }
        public MerchandiseOrderDetail Detail { get; }
        public MerchandiseOrderDeliverySchedule A { get; }
        public MerchandiseOrderDeliverySchedule B { get; }
        public IOptions<DeliveryScheduleOptions> Options { get; } = Microsoft.Extensions.Options.Options.Create(new DeliveryScheduleOptions { StorageReady = true, EnableMultiScheduleUi = true });
        public DeliveryScheduleService Service { get; }
        public DeliveryScheduleFulfillmentService Fulfillment { get; }
        public NotificationCapture? Notifications { get; }
        public Fixture(bool notify = false)
        {
            Db = DispatchProxy.Create<IDeliveryScheduleDbContext, Store>(); Store = (Store)(object)Db;
            Order = new() { MerchandiseOrderId = Guid.NewGuid(), CompanyId = Company, CustomerId = Guid.NewGuid(), IsActive = true, Status = "New" };
            Detail = new() { MerchandiseOrderDetailId = Guid.NewGuid(), MerchandiseOrderId = Order.MerchandiseOrderId, MerchandiseOrder = Order,
                IsActive = true, ExpectedQuantity = 1000, UnitPriceAgreed = 12345, DeliveryRequestDate = Clock.NowValue };
            A = new() { MerchandiseOrderDeliveryScheduleId = Guid.NewGuid(), MerchandiseOrderDetailId = Detail.MerchandiseOrderDetailId,
                MerchandiseOrderDetail = Detail, Quantity = 400, RequestedDate = Clock.NowValue };
            B = new() { MerchandiseOrderDeliveryScheduleId = Guid.NewGuid(), MerchandiseOrderDetailId = Detail.MerchandiseOrderDetailId,
                MerchandiseOrderDetail = Detail, Quantity = 600, RequestedDate = Clock.NowValue.AddDays(5) };
            Store.Seed(nameof(Db.MerchandiseOrders), Order); Store.Seed(nameof(Db.MerchandiseOrderDetails), Detail);
            Store.Seed(nameof(Db.MerchandiseOrderDeliverySchedules), A, B);
            var visibility = DispatchProxy.Create<ICustomerVisibilityService, ScopeProxy>(); Scope = (ScopeProxy)(object)visibility;
            Scope.CustomerId = Order.CustomerId; Scope.Scope = new(Company, User.EmployeeId!.Value, false, false, new HashSet<Guid>(), new HashSet<Guid>(), Clock.NowValue);
            Fulfillment = new(Db, Options);
            DeliverySchedulePromiseNotifier? notifier = null;
            if (notify)
            {
                Order.ExternalId = "DHG-test";
                Order.ManagerById = Guid.NewGuid();
                Store.Seed(nameof(Db.Employees), new HRM.Domain.Entities.HrSchema.Employee { EmployeeId = Order.ManagerById, CompanyId = Company, IsActive = true });
                var publisher = DispatchProxy.Create<HRM.Application.Features.Notifications.Services.INotificationService, NotificationCapture>();
                Notifications = (NotificationCapture)(object)publisher;
                notifier = new(Db, User, publisher);
            }
            Service = new(Db, User, new CurrentUserPermissionService(User), visibility, new Clock(), Options, Fulfillment, notifier);
        }
        public DeliveryScheduleInput Input(MerchandiseOrderDeliverySchedule row) => new(row.MerchandiseOrderDeliveryScheduleId, row.Quantity,
            DateOnly.FromDateTime(row.RequestedDate), row.PromisedDate.HasValue ? DateOnly.FromDateTime(row.PromisedDate.Value) : null, row.RequestNote, row.PromiseNote);
    }
    public class Store : ProductionOrderTestStore
    {
        public Action? BeforeSave { get; set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == "SaveChangesAsync") BeforeSave?.Invoke();
            return method.Name switch { "LockDeliveryScheduleAsync" => Task.CompletedTask,
                "get_DeliveryScheduleTransactionId" => Transaction.TransactionId,
                "BeginDeliveryScheduleTransactionAsync" => Task.FromResult<IDbContextTransaction>(Transaction), _ => base.Invoke(method, args) };
        }
    }
    public class NotificationCapture : DispatchProxy
    {
        public bool ThrowOnPublish { get; set; }
        public List<HRM.Application.Features.Notifications.Dtos.PublishNotificationRequest> Published { get; } = [];
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (ThrowOnPublish) throw new InvalidOperationException("Simulated notification failure");
            Published.Add((HRM.Application.Features.Notifications.Dtos.PublishNotificationRequest)args![0]!);
            return Task.FromResult(Guid.NewGuid());
        }
    }
    public class ScopeProxy : DispatchProxy
    {
        public bool HonorFullView { get; set; }
        public Guid CustomerId { get; set; }
        public ViewerScope Scope { get; set; } = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch {
            "BuildScopeAsync" or "BuildReadScopeAsync" => Task.FromResult(Scope),
            "ApplyMerchandiseOrderVisibility" => HonorFullView && ((ViewerScope)args![2]!).HasFullCustomerView
                ? (IQueryable<MerchandiseOrder>)args[0]! : ((IQueryable<MerchandiseOrder>)args![0]!).Where(x => x.CustomerId == CustomerId),
            _ => throw new NotSupportedException(method.Name) };
    }
    private sealed class Clock : IDateTimeProvider { public static readonly DateTime NowValue = new(2026, 11, 18); public DateTime Now => NowValue; }
}
