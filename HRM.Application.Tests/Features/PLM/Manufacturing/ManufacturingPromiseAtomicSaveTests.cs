using System.Reflection;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Abstractions.Persistence.PLM.DeliverySchedules;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.Manufacturing.Commands.ExecuteProductionOrderEditorAction;
using HRM.Application.Features.PLM.Manufacturing.Commands.UpdateProductionOrderInform;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;
using HRM.Application.Features.PLM.Manufacturing.Services.ProductionOrders;
using HRM.Application.Features.PLM.SaleOrders.DeliverySchedules;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Entities.OrderSchema;
using Microsoft.Extensions.Options;

namespace HRM.Application.Tests.Features.PLM.Manufacturing;

public sealed class ManufacturingPromiseAtomicSaveTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NotificationIsQueuedInsideTheSameTransactionWhichMayStillRollback(bool finalSaveFails)
    {
        var f = new Fixture(true);
        var current = (await f.Service.GetAsync(f.Sale.MerchandiseOrderId, f.Detail.MerchandiseOrderDetailId, default))!;
        f.Store.FailFinalSave = finalSaveFails;
        var revision = await ManufacturingEditorRevision.ReadAsync(f.Mfg, f.Order, default);
        var result = await f.Handler.Handle(new(f.Order.MfgProductionOrderId, new() { TotalQuantity = 100, NumOfBatches = 2, QcCheck = "new QC" })
        { IsEditorSave = true, ExpectedRevision = revision, EditorPatch = new() { Action = "Save", Revision = revision,
            DeliveryPromises = [new(f.Sale.MerchandiseOrderId, f.Detail.MerchandiseOrderDetailId, current.Revision, [new(f.A.MerchandiseOrderDeliveryScheduleId, new(2026, 12, 1))])] } }, default);
        Assert.Equal(!finalSaveFails, result.Success);
        Assert.Equal(!finalSaveFails, f.Store.Transaction.Committed);
        Assert.Equal(finalSaveFails, f.Store.Transaction.RolledBack);
        Assert.Equal(1, f.Notifications!.Count);
        Assert.False(f.Notifications.WasCommittedDuringPublish);
    }

    public class NotificationIntent : DispatchProxy
    {
        public Store Store { get; set; } = null!;
        public int Count { get; private set; }
        public bool WasCommittedDuringPublish { get; private set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            Count++;
            WasCommittedDuringPublish = Store.Transaction.Committed;
            return Task.FromResult(Guid.NewGuid());
        }
    }
    [Theory]
    [InlineData("ok", true)]
    [InlineData("stale", false)]
    [InlineData("final-save-fails", false)]
    public async Task StockRequestSavesPromisesInsideItsExistingTransaction(string scenario, bool success)
    {
        var f = new Fixture();
        var current = (await f.Service.GetAsync(f.Sale.MerchandiseOrderId, f.Detail.MerchandiseOrderDetailId, default))!;
        var patch = new ManufacturingDeliveryPromisePatch(f.Sale.MerchandiseOrderId, f.Detail.MerchandiseOrderDetailId,
            scenario == "stale" ? "stale" : current.Revision, [new(f.A.MerchandiseOrderDeliveryScheduleId, new(2026, 12, 1))]);
        f.Store.FailFinalSave = scenario == "final-save-fails";
        var events = DispatchProxy.Create<HRM.Application.Features.Timeline.Services.IEventLogWriter, Events>();
        var handler = new HRM.Application.Features.PLM.Manufacturing.Commands.UseProductionOrderStock.UseProductionOrderStockCommandHandler(
            f.Mfg, f.User, new CurrentUserPermissionService(f.User), new Clock(), events,
            PromisedDeliveryTestFactory.Create(f.Mfg, f.User).Notifier, f.Service);
        var result = await handler.Handle(new(f.Order.MfgProductionOrderId, new()
        { TotalQuantity = 100, NumOfBatches = 2, StepOfProduct = HRM.Domain.Enums.Manufacturings.StepOfProduct.Forsb })
        { DeliveryPromises = [patch] }, default);
        Assert.Equal(success, result.Success);
        Assert.Equal(success, f.Store.Transaction.Committed);
        Assert.Equal(!success, f.Store.Transaction.RolledBack);
        Assert.Equal(1, f.Store.Begins);
        Assert.Equal(0, f.Store.NestedBegins);
        if (success) { Assert.Equal("Stocked", f.Order.Status); Assert.Equal(new DateTime(2026, 12, 1), f.A.PromisedDate); }
    }

    [Theory]
    [InlineData("ok", true)]
    [InlineData("stale", false)]
    [InlineData("final-save-fails", false)]
    public async Task FormulaRequestSavesPromisesInsideItsExistingTransaction(string scenario, bool success)
    {
        var f = new ManufacturingFormulaCreationTests.Fixture(HRM.Domain.Enums.Manufacturings.ManufacturingFormulaOrigin.FromVu, true);
        var current = (await f.DeliveryService!.GetAsync(f.Sale.MerchandiseOrderId, f.Detail.MerchandiseOrderDetailId, default))!;
        var patch = new ManufacturingDeliveryPromisePatch(f.Sale.MerchandiseOrderId, f.Detail.MerchandiseOrderDetailId,
            scenario == "stale" ? "stale" : current.Revision, [new(current.Items[0].Id, new(2026, 12, 1))]);
        ((Store)f.Store).FailFinalSave = scenario == "final-save-fails";
        var result = await f.Handler.Handle(new(f.Order.MfgProductionOrderId, f.Request()) { DeliveryPromises = [patch] }, default);
        Assert.Equal(success, result.Success);
        Assert.Equal(success, f.Store.Transaction.Committed);
        Assert.Equal(!success, f.Store.Transaction.RolledBack);
        Assert.Equal(1, ((Store)f.Store).Begins);
        Assert.Equal(0, ((Store)f.Store).NestedBegins);
        if (success) Assert.Equal(new DateOnly(2026, 12, 1), (await f.DeliveryService.GetAsync(f.Sale.MerchandiseOrderId, f.Detail.MerchandiseOrderDetailId, default))!.Items[0].PromisedDate);
    }

    public class Events : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) => Task.CompletedTask;
    }
    [Theory]
    [InlineData("ok", true)]
    [InlineData("stale-schedule", false)]
    [InlineData("foreign-link", false)]
    [InlineData("closed-sale", false)]
    [InlineData("final-save-fails", false)]
    [InlineData("no-promise-permission", false)]
    [InlineData("stale-mfg", false)]
    [InlineData("second-link-invalid", false)]
    [InlineData("legacy-no-patches", true)]
    public async Task MainSaveHasOneTransactionAndNeverCommitsHalfTheRequest(string scenario, bool success)
    {
        var f = new Fixture();
        var current = (await f.Service.GetAsync(f.Sale.MerchandiseOrderId, f.Detail.MerchandiseOrderDetailId, default))!;
        var patch = new ManufacturingDeliveryPromisePatch(f.Sale.MerchandiseOrderId,
            scenario == "foreign-link" ? Guid.NewGuid() : f.Detail.MerchandiseOrderDetailId,
            scenario == "stale-schedule" ? "stale" : current.Revision,
            [new(f.A.MerchandiseOrderDeliveryScheduleId, new(2026, 12, 1))]);
        if (scenario == "closed-sale") f.Sale.Status = "Delivered";
        if (scenario == "no-promise-permission")
        {
            f.User.HasExplicitPermissionSet = true;
            f.User.Permissions = [ApplicationPermissions.PLM.ViewProductionOrders, ApplicationPermissions.PLM.ManageProductionOrders];
        }
        f.Store.FailFinalSave = scenario == "final-save-fails";
        var revision = await ManufacturingEditorRevision.ReadAsync(f.Mfg, f.Order, default);
        var patches = scenario == "second-link-invalid"
            ? new[] { patch, patch with { DetailId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff") } }
            : new[] { patch };
        var result = await f.Handler.Handle(new(f.Order.MfgProductionOrderId, new()
        {
            TotalQuantity = 100, NumOfBatches = 2, QcCheck = "updated QC"
        }) { IsEditorSave = true, ExpectedRevision = scenario == "stale-mfg" ? "stale" : revision,
            EditorPatch = new() { Action = "Save", Revision = revision, QcCheck = "updated QC", DeliveryPromises = scenario == "legacy-no-patches" ? null : patches } }, default);
        Assert.Equal(success, result.Success);
        Assert.Equal(1, f.Store.Begins);
        Assert.Equal(0, f.Store.NestedBegins);
        if (scenario == "second-link-invalid") Assert.Equal(1, f.Store.Saves);
        Assert.Equal(success, f.Store.Transaction.Committed);
        Assert.Equal(!success, f.Store.Transaction.RolledBack);
        Assert.Equal(new DateTime(2026, 11, 27), f.A.RequestedDate);
        Assert.Equal(400, f.A.Quantity);
        if (success)
        {
            Assert.Equal("updated QC", f.Order.QcCheck);
            if (scenario != "legacy-no-patches") Assert.Equal(new DateTime(2026, 12, 1), f.A.PromisedDate);
            else Assert.Null(f.A.PromisedDate);
        }
    }

    [Fact]
    public async Task DoesNotAcceptAnUnrelatedTransaction()
    {
        var f = new Fixture();
        var result = await f.Service.SaveManufacturingPromisesAsync(f.Order.MfgProductionOrderId, [], Guid.NewGuid(), default);
        Assert.False(result.Success);
        Assert.Equal(0, f.Store.Saves);
    }

    public interface ICombined : IManufacturingDbContext, IDeliveryScheduleDbContext { }
    public class Store : ProductionOrderTestStore
    {
        public int Begins { get; private set; }
        public int NestedBegins { get; private set; }
        public bool FailFinalSave { get; set; }
        private int writes;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == "get_DeliveryScheduleTransactionId") return Begins > 0 ? Transaction.TransactionId : null;
            if (method.Name == "LockDeliveryScheduleAsync") return Task.CompletedTask;
            if (method.Name == "BeginTransactionAsync") Begins++;
            if (method.Name == "BeginDeliveryScheduleTransactionAsync") { NestedBegins++; throw new InvalidOperationException("Unexpected nested transaction"); }
            if (method.Name == "SaveChangesAsync" && ++writes == 2 && FailFinalSave) throw new InvalidOperationException("Failure after schedule write");
            return base.Invoke(method, args);
        }
    }
    private sealed class Fixture
    {
        public ProductionOrderCreationTests.TestUser User { get; } = new() { Roles = ["PLPUUser"] };
        public Store Store { get; }
        public IManufacturingDbContext Mfg { get; }
        public DeliveryScheduleService Service { get; }
        public UpdateProductionOrderInformCommandHandler Handler { get; }
        public MfgProductionOrder Order { get; }
        public MerchandiseOrder Sale { get; }
        public MerchandiseOrderDetail Detail { get; }
        public MerchandiseOrderDeliverySchedule A { get; }
        public NotificationIntent? Notifications { get; }
        public Fixture(bool notify = false)
        {
            var combined = DispatchProxy.Create<ICombined, Store>(); Store = (Store)(object)combined;
            Mfg = combined;
            var schedules = (IDeliveryScheduleDbContext)combined;
            Order = new() { MfgProductionOrderId = Guid.NewGuid(), CompanyId = User.CompanyId!.Value, IsActive = true, Status = "New", TotalQuantity = 100, NumOfBatches = 2 };
            Sale = new() { MerchandiseOrderId = Guid.NewGuid(), CompanyId = Order.CompanyId, IsActive = true, Status = "Approved" };
            Detail = new() { MerchandiseOrderDetailId = Guid.NewGuid(), MerchandiseOrderId = Sale.MerchandiseOrderId, MerchandiseOrder = Sale, IsActive = true, ExpectedQuantity = 1000, DeliveryRequestDate = new(2026, 11, 27) };
            A = new() { MerchandiseOrderDeliveryScheduleId = Guid.NewGuid(), MerchandiseOrderDetailId = Detail.MerchandiseOrderDetailId, MerchandiseOrderDetail = Detail, RequestedDate = new(2026, 11, 27), Quantity = 400, IsActive = true };
            var b = new MerchandiseOrderDeliverySchedule { MerchandiseOrderDeliveryScheduleId = Guid.NewGuid(), MerchandiseOrderDetailId = Detail.MerchandiseOrderDetailId, MerchandiseOrderDetail = Detail, RequestedDate = new(2026, 11, 29), Quantity = 600, IsActive = true };
            Store.Seed(nameof(Mfg.MfgProductionOrders), Order);
            Store.Seed(nameof(Mfg.MerchandiseOrders), Sale);
            Store.Seed(nameof(schedules.MerchandiseOrderDetails), Detail);
            Store.Seed(nameof(schedules.MerchandiseOrderDeliverySchedules), A, b);
            Store.Seed(nameof(Mfg.MfgOrderPOs), new MfgOrderPO { MfgProductionOrderId = Order.MfgProductionOrderId, ProductionOrder = Order, Detail = Detail, IsActive = true });
            var permissions = new CurrentUserPermissionService(User);
            var options = Options.Create(new DeliveryScheduleOptions { StorageReady = true, EnableMultiScheduleUi = true });
            DeliverySchedulePromiseNotifier? notifier = null;
            if (notify)
            {
                Sale.ExternalId = "DHG-test";
                Sale.ManagerById = Guid.NewGuid();
                Store.Seed(nameof(schedules.Employees), new HRM.Domain.Entities.HrSchema.Employee { EmployeeId = Sale.ManagerById, CompanyId = Sale.CompanyId, IsActive = true });
                var publisher = DispatchProxy.Create<HRM.Application.Features.Notifications.Services.INotificationService, NotificationIntent>();
                Notifications = (NotificationIntent)(object)publisher;
                Notifications.Store = Store;
                notifier = new(schedules, User, publisher);
            }
            Service = new(schedules, User, permissions, null!, new Clock(), options, new(schedules, options), notifier);
            Handler = new(Mfg, User, permissions, new Clock(), null!, null!, null!, null!, PromisedDeliveryTestFactory.Create(Mfg, User).Notifier, Service);
        }
    }
    private sealed class Clock : IDateTimeProvider { public DateTime Now => new(2026, 11, 27); }
}
