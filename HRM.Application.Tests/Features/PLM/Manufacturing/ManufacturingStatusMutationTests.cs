using HRM.Application.Features.PLM.Manufacturing.Commands.ApplyProductionOrderAction;
using HRM.Application.Features.PLM.Manufacturing.Services.ProductionOrders;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.Manufacturing.Commands.FinishProductionOrder;
using HRM.Application.Features.PLM.Manufacturing.Commands.UpdateProductionOrderInformation;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;
using HRM.Application.Features.Timeline.Dtos;
using HRM.Application.Features.Timeline.Services;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Enums.Manufacturings;
using System.Text.Json;

namespace HRM.Application.Tests.Features.PLM.Manufacturing;

public sealed class ManufacturingStatusMutationTests
{
    [Theory]
    [InlineData(19, 20, 1)]
    [InlineData(20, 19, 1)]
    [InlineData(20, 20, 0)]
    public async Task DateUpdatePublishesActualBeforeAndAfterThroughSharedTopic(int oldDay, int newDay, int count)
    {
        var f = new Fixture();
        EnableDateNotifications(f);
        f.Order.ExpectedDate = new DateTime(2026, 10, oldDay, 15, 30, 0);
        var next = new DateTime(2026, 10, newDay);
        var result = await f.Update.Handle(new(f.Order.MfgProductionOrderId, new() {
            TotalQuantity = 100, NumOfBatches = 2, StepOfProduct = null, ExpectedDate = next }), default);
        Assert.True(result.Success);
        Assert.True(f.Store.Transaction.Committed);
        Assert.Equal(next, f.Order.ExpectedDate);
        Assert.Equal(next, f.Schedule.DeliveryPlanDate);
        Assert.Equal(count, f.Notifications.Published.Count);
        if (count == 0) return;
        var notification = Assert.Single(f.Notifications.Published);
        Assert.Equal(HRM.Domain.Enums.Notifications.TopicNotifications.MerchandiseOrderPromisedDeliveryNotified, notification.Topic);
        Assert.Contains(f.SaleOrder.ManagerById, notification.TargetUserIds!);
        using var payload = JsonDocument.Parse(notification.PayloadJson!);
        Assert.Equal($"2026-10-{oldDay:D2}", payload.RootElement.GetProperty("previousPromisedDeliveryDate").GetString());
        Assert.Equal($"2026-10-{newDay:D2}", payload.RootElement.GetProperty("promisedDeliveryDate").GetString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DateUpdateDoesNotCommitWhenSaveOrPublishFails(bool publishFails)
    {
        var f = new Fixture();
        EnableDateNotifications(f);
        f.Order.ExpectedDate = new DateTime(2026, 10, 19);
        f.Store.ThrowOnSave = !publishFails;
        f.Notifications.ThrowOnPublish = publishFails;
        var result = await f.Update.Handle(new(f.Order.MfgProductionOrderId, new() {
            TotalQuantity = 100, NumOfBatches = 2, StepOfProduct = null, ExpectedDate = new DateTime(2026, 10, 20) }), default);
        Assert.False(result.Success);
        Assert.False(f.Store.Transaction.Committed);
        Assert.True(f.Store.Transaction.RolledBack);
        Assert.Empty(f.Notifications.Published);
    }

    private static void EnableDateNotifications(Fixture f)
    {
        f.Order.ExternalId = "MFG_DATE";
        f.SaleOrder.ExternalId = "DHG_DATE";
        f.SaleOrder.ManagerById = Guid.NewGuid();
        f.Db.MfgOrderPOs.Single().Detail.IsActive = true;
        f.Store.Seed(nameof(f.Db.Employees), new HRM.Domain.Entities.HrSchema.Employee {
            EmployeeId = f.SaleOrder.ManagerById, CompanyId = f.Order.CompanyId, IsActive = true });
    }

    [Theory]
    [InlineData("New", "UseStock", true)]
    [InlineData("New", "RequestFormula", true)]
    [InlineData("New", "Schedule", false)]
    [InlineData("FormulaSuccess", "Schedule", true)]
    [InlineData("FormulaSuccess", "RequestFormula", true)]
    [InlineData("FormulaSuccess", "UseStock", false)]
    [InlineData("FormulaRequested", "Schedule", false)]
    [InlineData("Scheduling", "SavePlan", true)]
    [InlineData("Stocked", "SavePlan", true)]
    [InlineData("Finished", "Schedule", false)]
    [InlineData("Finished", "RequestFormula", false)]
    [InlineData("Unknown", "SavePlan", false)]
    public void BasicActions_EnforceStatusPolicyPreviouslyHeldInFeButtons(string status, string action, bool allowed)
    {
        var parsed = Enum.Parse<HRM.Application.Features.PLM.Manufacturing.Commands.ApplyProductionOrderAction.ManufacturingInformationAction>(action);
        Assert.Equal(allowed, HRM.Application.Features.PLM.Manufacturing.Services.ProductionOrders.ManufacturingInformationActionRules.CanApply(status, parsed));
    }

    [Theory]
    [InlineData("New")]
    [InlineData("Finished")]
    public async Task BasicSchedule_RejectsForbiddenStatusBeforeChangingOrder(string status)
    {
        var f = new Fixture();
        f.Order.Status = status;
        f.Db.MfgOrderPOs.Single().Detail.IsActive = true;
        var result = await f.Update.Handle(new(f.Order.MfgProductionOrderId, new() { TotalQuantity = 100, NumOfBatches = 2,
            StepOfProduct = StepOfProduct.Forsb, ExpectedDate = new DateTime(2026, 10, 20) },
            HRM.Application.Features.PLM.Manufacturing.Commands.ApplyProductionOrderAction.ManufacturingInformationAction.Schedule), default);
        Assert.False(result.Success);
        Assert.Equal(status, f.Order.Status);
        Assert.Equal(0, f.Store.Saves);
        Assert.Empty(f.Store.Added);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BasicSchedule_ReservesServerQuantitiesInSameTransactionOrRollsBack(bool saveSucceeds)
    {
        var f = new Fixture();
        f.Order.Status = "FormulaSuccess";
        var link = f.Db.MfgOrderPOs.Single();
        link.Detail.IsActive = true;
        var formula = new ManufacturingFormula { ManufacturingFormulaId = Guid.NewGuid(), CompanyId = f.Order.CompanyId, IsActive = true };
        var item = new ManufacturingFormulaMaterial { ManufacturingFormulaMaterialId = Guid.NewGuid(), ManufacturingFormulaId = formula.ManufacturingFormulaId,
            ManufacturingFormula = formula, IsActive = true, Quantity = .5m, MaterialExternalIdSnapshot = "SERVER-CODE",
            itemType = HRM.Domain.Enums.Formulas.ItemType.Material, MaterialId = Guid.NewGuid() };
        f.Store.Seed(nameof(f.Db.ManufacturingFormulaMaterials), item);
        f.Store.Seed(nameof(f.Db.ProductionSelectVersions), new ProductionSelectVersion {
            CompanyId = f.Order.CompanyId, MfgProductionOrderId = f.Order.MfgProductionOrderId, ManufacturingFormula = formula,
            ManufacturingFormulaId = formula.ManufacturingFormulaId });
        f.Store.ThrowOnSave = !saveSucceeds;
        var result = await f.Update.Handle(new(f.Order.MfgProductionOrderId, new() { TotalQuantity = 200, NumOfBatches = 2,
            Status = "Finished", StepOfProduct = StepOfProduct.Forsb, ExpectedDate = new DateTime(2026, 10, 20) }, HRM.Application.Features.PLM.Manufacturing.Commands.ApplyProductionOrderAction.ManufacturingInformationAction.Schedule), default);
        Assert.Equal(saveSucceeds, result.Success);
        Assert.Equal(saveSucceeds, f.Store.Transaction.Committed);
        if (!saveSucceeds) Assert.True(f.Store.Transaction.RolledBack);
        Assert.Equal("Scheduling", f.Order.Status);
        Assert.Equal(new Clock().Now, f.Db.ProductionSelectVersions.Single().ValidFrom);
        var reservation = Assert.Single(f.Store.Added.OfType<HRM.Domain.Entities.WarehouseSchema.WarehouseTempStock>());
        Assert.Equal("SERVER-CODE", reservation.Code);
        Assert.Equal(100m, reservation.QtyRequest);
        Assert.Equal("Processing", f.SaleOrder.Status);
    }

    [Theory]
    [InlineData("no-va")]
    [InlineData("inactive-link")]
    [InlineData("foreign-va")]
    public async Task BasicSchedule_RejectsUnavailableSelectionBeforeMutation(string reason)
    {
        var f = new Fixture();
        f.Order.Status = "FormulaSuccess";
        f.Db.MfgOrderPOs.Single().Detail.IsActive = reason != "inactive-link";
        if (reason == "foreign-va") f.Store.Seed(nameof(f.Db.ProductionSelectVersions), new ProductionSelectVersion {
            CompanyId = f.Order.CompanyId, MfgProductionOrderId = f.Order.MfgProductionOrderId,
            ManufacturingFormula = new ManufacturingFormula { CompanyId = Guid.NewGuid(), IsActive = true } });
        var result = await f.Update.Handle(new(f.Order.MfgProductionOrderId, new() { TotalQuantity = 100, NumOfBatches = 2,
            StepOfProduct = StepOfProduct.Forsb, ExpectedDate = new DateTime(2026, 10, 20) },
            HRM.Application.Features.PLM.Manufacturing.Commands.ApplyProductionOrderAction.ManufacturingInformationAction.Schedule), default);
        Assert.False(result.Success);
        Assert.Equal("FormulaSuccess", f.Order.Status);
        Assert.Equal(0, f.Store.Saves);
        Assert.Empty(f.Store.Added);
    }

    [Fact]
    public async Task BasicStock_UsesServerStatusAndRetainsLegacyDetailCreatedDate()
    {
        var f = new Fixture();
        f.Db.MfgOrderPOs.Single().Detail.IsActive = true;
        var created = f.Order.CreatedDate;
        var result = await f.Update.Handle(new(f.Order.MfgProductionOrderId, new() { TotalQuantity = 10, Status = "Finished" },
            HRM.Application.Features.PLM.Manufacturing.Commands.ApplyProductionOrderAction.ManufacturingInformationAction.UseStock), default);
        Assert.True(result.Success, result.Message);
        Assert.Equal("Stocked", f.Order.Status);
        Assert.Equal(created, f.Order.CreatedDate);
        Assert.Equal("Xuất tồn kho", f.Order.PlpuNote);
        Assert.Empty(f.Store.Added.OfType<HRM.Domain.Entities.WarehouseSchema.WarehouseTempStock>());
    }

    [Fact]
    public async Task Finish_SavesOrderScheduleAndTimelineAtomically()
    {
        var f = new Fixture();
        var result = await f.Finish.Handle(new(f.Order.MfgProductionOrderId), default);
        Assert.True(result.Success);
        Assert.Equal("Finished", f.Order.Status);
        Assert.Equal("Finished", f.Schedule.Status);
        Assert.Equal(f.User.EmployeeId, f.Order.UpdatedBy);
        Assert.Equal("Finished", Assert.Single(f.Timeline.Events).Status);
        Assert.True(f.Store.Transaction.Committed);
    }

    [Fact]
    public async Task Finish_AlreadyFinishedDoesNotSaveOrWriteTimeline()
    {
        var f = new Fixture();
        f.Order.Status = "Finished";
        Assert.True((await f.Finish.Handle(new(f.Order.MfgProductionOrderId), default)).Success);
        Assert.Equal(0, f.Store.Saves);
        Assert.Empty(f.Timeline.Events);
    }

    [Fact]
    public async Task Finish_SaveFailureRollsBack()
    {
        var f = new Fixture();
        f.Store.ThrowOnSave = true;
        Assert.False((await f.Finish.Handle(new(f.Order.MfgProductionOrderId), default)).Success);
        Assert.True(f.Store.Transaction.RolledBack);
        Assert.False(f.Store.Transaction.Committed);
    }

    [Fact]
    public async Task Update_StatusChangeSyncsSaleOrderAndClearsNullableValuesLikeLegacy()
    {
        var f = new Fixture();
        var selection = f.PendingSelection();
        f.Order.TotalQuantity = 200;
        f.Order.NumOfBatches = 2;
        f.Order.LabNote = "Lab";
        f.Order.StepOfProduct = Enum.GetValues<StepOfProduct>().First();
        var expected = new DateTime(2026, 10, 8);
        var result = await f.Update.Handle(new(f.Order.MfgProductionOrderId, new()
        {
            StepOfProduct = null, Status = "Scheduling", ExpectedDate = expected
        }), default);
        Assert.True(result.Success);
        Assert.Equal("Processing", f.SaleOrder.Status);
        Assert.Equal(expected, f.Schedule.DeliveryPlanDate);
        Assert.Null(f.Order.TotalQuantity);
        Assert.Null(f.Order.NumOfBatches);
        Assert.Equal("Lab", f.Order.LabNote);
        Assert.Null(f.Order.StepOfProduct);
        Assert.Equal(2, f.Timeline.Events.Count);
        Assert.True(f.Store.Transaction.Committed);
        Assert.Equal(new Clock().Now, selection.ValidFrom);
    }

    [Fact]
    public async Task Update_SchedulingCreatesScheduleOnlyOnceAndDoesNotReserveStock()
    {
        var f = new Fixture();
        var selection = f.PendingSelection();
        f.Store.Seed<SchedualMfg>(nameof(f.Db.SchedualMfgs));
        var result = await f.Update.Handle(new(f.Order.MfgProductionOrderId, new()
        {
            StepOfProduct = null, Status = "Scheduling", LabNote = "L", PlpuNote = "P"
        }), default);
        Assert.True(result.Success);
        Assert.Equal(new Clock().Now, selection.ValidFrom);
        var schedule = Assert.Single(f.Store.Added.OfType<SchedualMfg>());
        Assert.Equal("L", schedule.Note);
        Assert.Null(schedule.StepOfProduct);
        Assert.Single(f.Store.Added);
    }

    [Fact]
    public async Task CrossCompanyLinkAndSaleUserAreRejected()
    {
        var f = new Fixture();
        f.Order.CompanyId = Guid.NewGuid();
        Assert.False((await f.Finish.Handle(new(f.Order.MfgProductionOrderId), default)).Success);
        Assert.False((await f.Update.Handle(new(f.Order.MfgProductionOrderId, new()), default)).Success);
        Assert.Equal(0, f.Store.Saves);
        f.User.Roles = [ApplicationRoles.Sales.SaleUser];
        Assert.False((await f.Finish.Handle(new(f.Order.MfgProductionOrderId), default)).Success);
    }

    [Fact]
    public void InformationRequest_RequiresExplicitStepIncludingNull()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<UpdateProductionOrderInformationRequest>("{}"));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<UpdateProductionOrderInformationRequest>("{\"StepOfProduct\":null}"));
        Assert.NotNull(JsonSerializer.Deserialize<UpdateProductionOrderInformationRequest>("{\"StepOfProduct\":null,\"ExpectedDate\":null,\"TotalQuantity\":null,\"NumOfBatches\":null}"));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("foreign")]
    [InlineData("inactive")]
    public async Task LegacyScheduling_CannotCommitWithoutEligibleVa(string reason)
    {
        var f = new Fixture();
        if (reason != "missing")
        {
            var selected = f.PendingSelection();
            if (reason == "foreign") selected.ManufacturingFormula!.CompanyId = Guid.NewGuid();
            else selected.ManufacturingFormula!.IsActive = false;
        }
        var result = await f.Update.Handle(new(f.Order.MfgProductionOrderId,
            new() { Status = "Scheduling", StepOfProduct = null }), default);
        Assert.False(result.Success);
        Assert.False(f.Store.Transaction.Committed);
        Assert.Equal(0, f.Store.Saves);
    }

    private sealed class Fixture
    {
        public ProductionSelectVersion PendingSelection()
        {
            var formula = new ManufacturingFormula { ManufacturingFormulaId = Guid.NewGuid(), CompanyId = Order.CompanyId, IsActive = true };
            var selection = new ProductionSelectVersion { CompanyId = Order.CompanyId, MfgProductionOrderId = Order.MfgProductionOrderId,
                ManufacturingFormulaId = formula.ManufacturingFormulaId, ManufacturingFormula = formula };
            Store.Seed(nameof(Db.ProductionSelectVersions), selection);
            return selection;
        }
        public ProductionOrderCreationTests.TestUser User { get; } = new();
        public IManufacturingDbContext Db { get; }
        public ProductionOrderTestStore Store { get; }
        public MfgProductionOrder Order { get; }
        public MerchandiseOrder SaleOrder { get; }
        public SchedualMfg Schedule { get; }
        public Timeline Timeline { get; } = new();
        public FinishProductionOrderCommandHandler Finish { get; }
        public UpdateProductionOrderInformationCommandHandler Update { get; }
        public NotifyPromisedDeliveryTests.CapturePort Notifications { get; }
        public Fixture()
        {
            (Db, Store) = ProductionOrderTestStore.CreateContext<IManufacturingDbContext>();
            Order = new() { MfgProductionOrderId = Guid.NewGuid(), CompanyId = User.CompanyId!.Value, IsActive = true, Status = "New" };
            SaleOrder = new() { MerchandiseOrderId = Guid.NewGuid(), CompanyId = User.CompanyId.Value, Status = "Approved", IsActive = true };
            var detail = new MerchandiseOrderDetail { MerchandiseOrderDetailId = Guid.NewGuid(), MerchandiseOrderId = SaleOrder.MerchandiseOrderId, MerchandiseOrder = SaleOrder };
            Store.Seed(nameof(Db.MfgOrderPOs), new MfgOrderPO { MfgProductionOrderId = Order.MfgProductionOrderId, ProductionOrder = Order, Detail = detail, IsActive = true });
            Store.Seed(nameof(Db.MerchandiseOrders), SaleOrder);
            Schedule = new() { MfgProductionOrderId = Order.MfgProductionOrderId, Status = "Scheduling" };
            Store.Seed(nameof(Db.SchedualMfgs), Schedule);
            var permissions = new CurrentUserPermissionService(User);
            Finish = new(Db, User, permissions, new Clock(), Timeline);
            var notification = PromisedDeliveryTestFactory.Create(Db, User);
            Notifications = notification.Capture;
            Update = new(Db, User, permissions, new Clock(), Timeline,
                new HRM.Application.Features.PLM.ProductionOrders.Services.ProductionOrderReservationService(Db), notification.Notifier);
        }
    }
    private sealed class Clock : IDateTimeProvider { public DateTime Now => new(2026, 10, 5, 12, 0, 0); }
    private sealed class Timeline : IEventLogWriter
    {
        public List<EventLogCreateRequest> Events { get; } = [];
        public Task AddAsync(EventLogCreateRequest request, CancellationToken ct = default) { Events.Add(request); return Task.CompletedTask; }
        public Task AddRangeAsync(IEnumerable<EventLogCreateRequest> requests, CancellationToken ct = default) { Events.AddRange(requests); return Task.CompletedTask; }
    }
}
