using HRM.Application.Features.PLM.Manufacturing.Services.Formulas;
using HRM.Application.Features.PLM.Manufacturing.Services.Notifications;
using System.Reflection;
using HRM.Application.Abstractions.Commons.ExternalIds;
using HRM.Application.Abstractions.Commons.Pricing;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Notifications.Dtos;
using HRM.Application.Features.Notifications.Services;
using HRM.Application.Features.PLM.Manufacturing.Commands.CreateProductionOrderFormula;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;

using HRM.Application.Features.PLM.ProductionOrders.Services;
using HRM.Application.Features.Timeline.Dtos;
using HRM.Application.Features.Timeline.Services;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.Formulas;
using HRM.Domain.Enums.Manufacturings;
using HRM.Domain.Enums.Merchadises;
using Xunit;

namespace HRM.Application.Tests.Features.PLM.Manufacturing;

public sealed class ManufacturingFormulaCreationTests
{
    [Theory]
    [InlineData("valid")]
    [InlineData("foreign-shelf")]
    [InlineData("inactive-shelf")]
    public async Task NewLot_RequiresActiveShelfInSameCompany(string reason)
    {
        var f = new Fixture(ManufacturingFormulaOrigin.Production);
        var row = f.Db.ManufacturingFormulaMaterials.Single();
        f.Store.Seed(nameof(f.Db.WarehouseShelfStocks), new HRM.Domain.Entities.WarehouseSchema.WarehouseShelfStock {
            CompanyId = f.Order.CompanyId, Code = "  " + f.Material.ExternalId + "  ", LotNo = "NEW-LOT",
            StockType = HRM.Domain.Enums.WareHouses.StockType.RawMaterial, WarehouseShelves = new() {
                CompanyId = reason == "foreign-shelf" ? Guid.NewGuid() : f.Order.CompanyId, IsActive = reason != "inactive-shelf" } });
        var result = await f.Handler.Handle(new(f.Order.MfgProductionOrderId, f.Request(items: [new() {
            SourceItemId = row.ManufacturingFormulaMaterialId, ItemId = f.Material.MaterialId,
            ItemType = ItemType.Material, Quantity = 1, LotNo = "NEW-LOT" }])), default);
        Assert.Equal(reason == "valid", result.Success);
        Assert.Equal(reason == "valid", f.Store.Transaction.Committed);
        if (reason != "valid") Assert.Empty(f.Store.Added);
    }

    [Fact]
    public async Task CopySource_PreservesSavedDefectiveLotEvenWhenItHasNoRemainingStock()
    {
        var f = new Fixture(ManufacturingFormulaOrigin.Production);
        f.User.Roles = [ApplicationRoles.Developer];
        var source = f.Db.ManufacturingFormulaMaterials.Single();
        source.LotNo = "SAVED-EMPTY";
        source.itemType = ItemType.MaterialFailure;
        var result = await f.Handler.Handle(new(f.Order.MfgProductionOrderId, f.Request()), default);
        Assert.True(result.Success, result.Message);
        var copied = Assert.Single(Assert.Single(f.Store.Added.OfType<ManufacturingFormula>()).ManufacturingFormulaMaterials);
        Assert.Equal("SAVED-EMPTY", copied.LotNo);
        Assert.Equal(ItemType.MaterialFailure, copied.itemType);
    }

    [Fact]
    public async Task ChangedLotWithoutStockProvenance_IsRejected()
    {
        var f = new Fixture(ManufacturingFormulaOrigin.Production);
        var source = f.Db.ManufacturingFormulaMaterials.Single();
        source.LotNo = "SAVED-EMPTY";
        var result = await f.Handler.Handle(new(f.Order.MfgProductionOrderId, f.Request(items: [new() {
            SourceItemId = source.ManufacturingFormulaMaterialId, ItemId = f.Material.MaterialId,
            ItemType = ItemType.Material, Quantity = 1, LotNo = "FORGED" }])), default);
        Assert.False(result.Success);
        Assert.Empty(f.Store.Added);
    }

    [Theory]
    [InlineData(ManufacturingFormulaOrigin.FromVu, true)]
    [InlineData(ManufacturingFormulaOrigin.Standard, true)]
    [InlineData(ManufacturingFormulaOrigin.ProductionOld, false)]
    [InlineData(ManufacturingFormulaOrigin.Production, false)]
    [InlineData(ManufacturingFormulaOrigin.Improvement, false)]
    public async Task Source_DeterminesSelectionStatusScheduleAndReservation(ManufacturingFormulaOrigin origin, bool effective)
    {
        var f = new Fixture(origin);
        var result = await f.Handler.Handle(new(f.Order.MfgProductionOrderId, f.Request()), default);
        Assert.True(result.Success, result.Message);
        var formula = Assert.Single(f.Store.Added.OfType<ManufacturingFormula>());
        var selection = Assert.Single(f.Store.Added.OfType<ProductionSelectVersion>());
        Assert.Equal(effective ? "Checking" : "New", formula.Status);
        Assert.Equal(effective ? "Scheduling" : "FormulaRequested", f.Order.Status);
        Assert.Equal(effective, selection.ValidFrom.HasValue);
        Assert.Null(selection.ValidTo);
        Assert.Equal(effective ? 1 : 0, f.Store.Added.OfType<SchedualMfg>().Count());
        Assert.Equal(effective ? 1 : 0, f.Store.Added.OfType<HRM.Domain.Entities.WarehouseSchema.WarehouseTempStock>().Count());
        Assert.Equal(12.5m, formula.TotalPrice);
        Assert.Equal("CURRENT-MATERIAL", Assert.Single(formula.ManufacturingFormulaMaterials).MaterialExternalIdSnapshot);
        Assert.Equal(Fixture.Now, f.Order.CreatedDate);
        Assert.Equal("Processing", f.Sale.Status);
        Assert.True(f.Store.Transaction.Committed);
        Assert.Empty(f.Notifications.Requests);
    }

    [Theory]
    [InlineData("company")]
    [InlineData("source")]
    [InlineData("status")]
    [InlineData("item-company")]
    public async Task InvalidReferenceOrState_DoesNotCreateFormula(string reason)
    {
        var f = new Fixture(ManufacturingFormulaOrigin.FromVu);
        var request = f.Request();
        if (reason == "company") f.Order.CompanyId = Guid.NewGuid();
        if (reason == "status") f.Order.Status = "Finished";
        if (reason == "source") request = f.Request(Guid.NewGuid());
        if (reason == "item-company") f.Material.CompanyId = Guid.NewGuid();
        var result = await f.Handler.Handle(new(f.Order.MfgProductionOrderId, request), default);
        Assert.False(result.Success);
        Assert.Empty(f.Store.Added);
        Assert.Equal(0, f.Store.Saves);
        Assert.True(f.Store.Transaction.RolledBack);
    }

    [Fact]
    public async Task SourcePrice_TriggersWarningWithoutLeakingCostInNotification()
    {
        var f = new Fixture(ManufacturingFormulaOrigin.FromVu);
        f.Detail.UnitPriceAgreed = 1;
        Assert.True((await f.Handler.Handle(new(f.Order.MfgProductionOrderId, f.Request()), default)).Success);
        var notification = Assert.Single(f.Notifications.Requests);
        Assert.Equal(f.Order.CompanyId, notification.CompanyId);
        Assert.Null(notification.PayloadJson);
        Assert.DoesNotContain("12.5", notification.Message);
        Assert.Equal(ApplicationRoleSets.Notifications.ManufacturingPriceWarningRecipients, notification.TargetRoles);
    }

    [Fact]
    public async Task SaveFailure_ReturnsFailureAndRollsBackTransaction()
    {
        var f = new Fixture(ManufacturingFormulaOrigin.FromVu);
        f.Store.ThrowOnSave = true;
        var result = await f.Handler.Handle(new(f.Order.MfgProductionOrderId, f.Request()), default);
        Assert.False(result.Success);
        Assert.True(f.Store.Transaction.RolledBack);
        Assert.False(f.Store.Transaction.Committed);
    }

    [Fact]
    public async Task ExistingEffectiveSelection_RejectsDuplicateFormulaCreation()
    {
        var f = new Fixture(ManufacturingFormulaOrigin.FromVu);
        f.Store.Seed(nameof(f.Db.ProductionSelectVersions), new ProductionSelectVersion
        { MfgProductionOrderId = f.Order.MfgProductionOrderId, CompanyId = f.Order.CompanyId, ValidFrom = Fixture.Now });
        Assert.False((await f.Handler.Handle(new(f.Order.MfgProductionOrderId, f.Request()), default)).Success);
        Assert.Empty(f.Store.Added);
    }

    internal sealed class Fixture
    {
        public static readonly DateTime Now = new(2026, 10, 10, 12, 0, 0);
        public ProductionOrderCreationTests.TestUser User { get; } = new();
        public IManufacturingDbContext Db { get; }
        public ProductionOrderTestStore Store { get; }
        public MfgProductionOrder Order { get; }
        public MerchandiseOrder Sale { get; }
        public MerchandiseOrderDetail Detail { get; }
        public Material Material { get; }
        public Guid SourceId { get; }
        public ManufacturingFormulaOrigin Origin { get; }
        public NotificationStub Notifications { get; }
        public CreateProductionOrderFormulaCommandHandler Handler { get; }
        public Fixture(ManufacturingFormulaOrigin origin)
        {
            Origin = origin;
            (Db, Store) = ProductionOrderTestStore.CreateContext<IManufacturingDbContext>();
            var product = new Product { ProductId = Guid.NewGuid(), CompanyId = User.CompanyId!.Value, IsActive = true, Requirement = "product requirement" };
            Order = new() { MfgProductionOrderId = Guid.NewGuid(), ProductId = product.ProductId, Product = product, CompanyId = product.CompanyId,
                IsActive = true, Status = "New", CreatedDate = Now.AddDays(-1), FormulaId = Guid.NewGuid() };
            Sale = new() { MerchandiseOrderId = Guid.NewGuid(), CompanyId = Order.CompanyId, IsActive = true, Status = "Approved", OrderType = OrderType.Merchandise };
            Detail = new() { MerchandiseOrderDetailId = Guid.NewGuid(), MerchandiseOrderId = Sale.MerchandiseOrderId, MerchandiseOrder = Sale,
                ProductId = product.ProductId, IsActive = true, UnitPriceAgreed = 20 };
            var category = new Category { CategoryId = Guid.NewGuid(), CompanyId = Order.CompanyId, IsActive = true };
            Material = new() { MaterialId = Guid.NewGuid(), CompanyId = Order.CompanyId, IsActive = true, CategoryId = category.CategoryId,
                ExternalId = "CURRENT-MATERIAL", Name = "current name", Unit = "kg" };
            var vu = new Formula { FormulaId = Order.FormulaId.Value, ProductId = product.ProductId, Product = product,
                CompanyId = Order.CompanyId, IsActive = true, Note = "source note" };
            var va = new ManufacturingFormula { ManufacturingFormulaId = Guid.NewGuid(), CompanyId = Order.CompanyId, IsActive = true,
                ExternalId = "SOURCE-VA", CreatedDate = origin == ManufacturingFormulaOrigin.ProductionOld ? new(2026, 2, 1) : Now };
            SourceId = origin is ManufacturingFormulaOrigin.FromVu or ManufacturingFormulaOrigin.Improvement ? vu.FormulaId : va.ManufacturingFormulaId;
            Store.Seed(nameof(Db.Products), product);
            Store.Seed(nameof(Db.Materials), Material);
            Store.Seed(nameof(Db.Categories), category);
            Store.Seed(nameof(Db.Formulas), vu);
            Store.Seed(nameof(Db.ManufacturingFormulas), va);
            Store.Seed(nameof(Db.MerchandiseOrders), Sale);
            Store.Seed(nameof(Db.MfgOrderPOs), new MfgOrderPO
            { MfgProductionOrderId = Order.MfgProductionOrderId, ProductionOrder = Order, Detail = Detail, IsActive = true });
            Store.Seed(nameof(Db.FormulaMaterials), new FormulaMaterial { FormulaMaterialId = Guid.NewGuid(), FormulaId = vu.FormulaId,
                Formula = vu, MaterialId = Material.MaterialId, CategoryId = category.CategoryId, Quantity = 1, UnitPrice = 12.5m,
                itemType = ItemType.Material, Unit = "kg", IsActive = true });
            Store.Seed(nameof(Db.ManufacturingFormulaMaterials), new ManufacturingFormulaMaterial { ManufacturingFormulaMaterialId = Guid.NewGuid(),
                ManufacturingFormulaId = va.ManufacturingFormulaId, ManufacturingFormula = va, MaterialId = Material.MaterialId,
                CategoryId = category.CategoryId, Quantity = 1, UnitPrice = 12.5m, itemType = ItemType.Material, Unit = "kg", IsActive = true });
            if (origin is ManufacturingFormulaOrigin.Standard or ManufacturingFormulaOrigin.Improvement)
                Store.Seed(nameof(Db.ProductStandardFormulas), new ProductStandardFormula
                { ProductId = product.ProductId, CompanyId = Order.CompanyId, ManufacturingFormulaId = va.ManufacturingFormulaId,
                    ManufacturingFormula = va, ValidFrom = Now.AddDays(-2) });
            if (origin == ManufacturingFormulaOrigin.Improvement)
                Store.Seed(nameof(Db.MfgProductionOrders), new MfgProductionOrder
                { MfgProductionOrderId = Guid.NewGuid(), ProductId = product.ProductId, CompanyId = Order.CompanyId, IsActive = true,
                    CreatedDate = Now.AddDays(-3), FormulaId = Guid.NewGuid() });
            if (origin is ManufacturingFormulaOrigin.Production or ManufacturingFormulaOrigin.ProductionOld)
                Store.Seed(nameof(Db.ProductionSelectVersions), new ProductionSelectVersion
                { CompanyId = Order.CompanyId, MfgProductionOrderId = Guid.NewGuid(), MfgProductionOrder = new MfgProductionOrder
                    { CompanyId = Order.CompanyId, ProductId = product.ProductId, IsActive = true },
                    ManufacturingFormulaId = va.ManufacturingFormulaId, ManufacturingFormula = va, ValidFrom = Now.AddDays(-2), ValidTo = Now.AddDays(-1) });
            var notifications = DispatchProxy.Create<INotificationService, NotificationStub>();
            Notifications = (NotificationStub)(object)notifications;
            var prices = DispatchProxy.Create<IMaterialPriceQueryService, PriceStub>();
            Handler = new(Db, User, new CurrentUserPermissionService(User), new Clock(), new Codes(), new Timeline(),
                new ManufacturingFormulaInputResolver(Db, prices, new Clock(), new CurrentUserPermissionService(User)), new ProductionOrderReservationService(Db), new ManufacturingWorkflowNotifier(notifications));
        }
        public CreateProductionOrderFormulaRequest Request(Guid? sourceId = null, IReadOnlyList<ManufacturingFormulaInputItem>? items = null)
            => new() { SourceFormulaId = sourceId ?? SourceId, Origin = Origin, TotalQuantity = 100, NumOfBatches = 2,
                StepOfProduct = Enum.GetValues<StepOfProduct>()[0], ExpectedDate = null, ManufacturingDate = null, Items = items };
    }
    public class NotificationStub : DispatchProxy
    {
        public List<PublishNotificationRequest> Requests { get; } = [];
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name != nameof(INotificationService.PublishAsync)) throw new NotSupportedException();
            Requests.Add((PublishNotificationRequest)args![0]!);
            return Task.FromResult(Guid.NewGuid());
        }
    }
    public class PriceStub : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args)
            => throw new InvalidOperationException("Unchanged source rows must keep source price without recalculating it.");
    }
    internal sealed class Clock : IDateTimeProvider { public DateTime Now => Fixture.Now; }
    private sealed class Codes : IExternalIdService
    {
        public Task<string> GenerateGlobalCodeAsync(Guid companyId, string prefix, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string> GenerateMonthlyCodeAsync(Guid companyId, string prefix, CancellationToken cancellationToken = default) => Task.FromResult(prefix + "261000001");
    }
    private sealed class Timeline : IEventLogWriter
    {
        public Task AddAsync(EventLogCreateRequest request, CancellationToken ct = default) => Task.CompletedTask;
        public Task AddRangeAsync(IEnumerable<EventLogCreateRequest> requests, CancellationToken ct = default) => Task.CompletedTask;
    }
}
