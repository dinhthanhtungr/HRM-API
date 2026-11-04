using HRM.Application.Features.PLM.Manufacturing.Services.Adjustments;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.Manufacturing.Commands.CheckProductionOrder;
using HRM.Application.Features.PLM.Manufacturing.Queries.GetProductionOrder;
using HRM.Application.Features.PLM.Manufacturing.Queries.GetProductionOrders;

using HRM.Application.Features.Pricing.Authorization;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.Manufacturings;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace HRM.Application.Tests.Features.PLM.Manufacturing;

public sealed class ManufacturingWorkflowTests
{
    [Theory]
    [InlineData("LabUser", true, true)]
    [InlineData("PLPUUser", false, false)]
    [InlineData("QCUser", true, true)]
    [InlineData("SaleUser", false, false)]
    public void LabAndPlpu_LotAndAdjustmentCapabilities(string role, bool defective, bool adjustment)
    {
        var permissions = new CurrentUserPermissionService(new ProductionOrderCreationTests.TestUser { Roles = [role] });
        Assert.Equal(defective, permissions.HasPermission(ApplicationPermissions.PLM.ViewManufacturingDefectiveLots));
        Assert.Equal(adjustment, permissions.HasPermission(ApplicationPermissions.PLM.ManageManufacturingAdjustments));
        if (role == "PLPUUser") Assert.True(permissions.HasPermission(ApplicationPermissions.PLM.ManageManufacturingColorChips));
    }
    [Theory]
    [InlineData("PLPUUser", true, true)]
    [InlineData("QCUser", true, true)]
    [InlineData("ManufactureUser", true, false)]
    [InlineData("LabUser", true, false)]
    [InlineData("SaleUser", false, false)]
    public void Capabilities_AreIndependent(string role, bool view, bool check)
    {
        var user = new ProductionOrderCreationTests.TestUser { Roles = [role] };
        var permissions = new CurrentUserPermissionService(user);
        Assert.Equal(view, permissions.HasPermission(ApplicationPermissions.PLM.ViewProductionOrders));
        Assert.Equal(check, permissions.HasPermission(ApplicationPermissions.PLM.CheckProductionOrders));
        Assert.False(permissions.HasPermission("plm.production-order.unknown"));
    }

    [Fact]
    public async Task Anonymous_FailsBeforeDatabaseAccess()
    {
        var user = new ProductionOrderCreationTests.TestUser { IsAuthenticated = false };
        var permissions = new CurrentUserPermissionService(user);
        Assert.False((await new GetProductionOrdersQueryHandler(null!, user, permissions).Handle(new(), default)).Success);
        Assert.False((await new GetProductionOrderQueryHandler(null!, user, permissions, null!).Handle(new(Guid.NewGuid()), default)).Success);
        Assert.False((await new CheckProductionOrderCommandHandler(null!, user, permissions, null!).Handle(new(Guid.NewGuid(), true), default)).Success);
    }

    [Fact]
    public async Task CrossCompanyOrder_CannotBeReadOrChecked()
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<IManufacturingDbContext>();
        var user = new ProductionOrderCreationTests.TestUser();
        var order = Order(Guid.NewGuid());
        store.Seed(nameof(db.MfgProductionOrders), order);
        var permissions = new CurrentUserPermissionService(user);
        Assert.False((await new GetProductionOrderQueryHandler(db, user, permissions, new Prices(false))
            .Handle(new(order.MfgProductionOrderId), default)).Success);
        Assert.False((await new CheckProductionOrderCommandHandler(db, user, permissions, new Clock())
            .Handle(new(order.MfgProductionOrderId, true), default)).Success);
        Assert.Null(order.Checker);
        Assert.Equal(0, store.Saves);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Detail_MasksPriceAndRejectsMultipleCurrentSelections(bool canViewPrice)
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<IManufacturingDbContext>();
        var user = new ProductionOrderCreationTests.TestUser();
        var order = Order(user.CompanyId!.Value);
        store.Seed(nameof(db.MfgProductionOrders), order);
        var handler = new GetProductionOrderQueryHandler(db, user, new CurrentUserPermissionService(user), new Prices(canViewPrice));
        var result = await handler.Handle(new(order.MfgProductionOrderId), default);
        Assert.True(result.Success);
        Assert.Equal(canViewPrice ? 123m : (decimal?)null, result.Data!.UnitPriceAgreed);
        store.Seed(nameof(db.ProductionSelectVersions),
            new ProductionSelectVersion { MfgProductionOrderId = order.MfgProductionOrderId, CompanyId = order.CompanyId },
            new ProductionSelectVersion { MfgProductionOrderId = order.MfgProductionOrderId, CompanyId = order.CompanyId });
        Assert.False((await handler.Handle(new(order.MfgProductionOrderId), default)).Success);
    }

    [Theory]
    [InlineData(true, true, "PO-123")]
    [InlineData(false, true, null)]
    [InlineData(true, false, null)]
    public async Task Detail_PoUsesLinkedOrderScope(bool active, bool sameCompany, string? expected)
    {
        var f = new ManufacturingFormulaCreationTests.Fixture(ManufacturingFormulaOrigin.FromVu);
        f.Store.Seed(nameof(f.Db.MfgProductionOrders), f.Order);
        f.Sale.PONo = "PO-123";
        f.Sale.IsActive = active;
        if (!sameCompany) f.Sale.CompanyId = Guid.NewGuid();
        var result = await new GetProductionOrderQueryHandler(f.Db, f.User,
            new CurrentUserPermissionService(f.User), new Prices(false)).Handle(new(f.Order.MfgProductionOrderId), default);
        Assert.True(result.Success, result.Message);
        Assert.Equal(expected, result.Data!.PoNo);
    }

    [Fact]
    public async Task Check_ValidFlagRecordsCheckerAndTimeAndNullIsRejected()
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<IManufacturingDbContext>();
        var user = new ProductionOrderCreationTests.TestUser();
        var order = Order(user.CompanyId!.Value);
        order.IsPrintedStock = true;
        store.Seed(nameof(db.MfgProductionOrders), order);
        var handler = new CheckProductionOrderCommandHandler(db, user, new CurrentUserPermissionService(user), new Clock());
        Assert.False((await handler.Handle(new(order.MfgProductionOrderId, null), default)).Success);
        Assert.Null(order.Checker);
        Assert.Equal(0, store.Saves);
        var result = await handler.Handle(new(order.MfgProductionOrderId, true), default);
        Assert.True(result.Success);
        Assert.True(order.IsPrintedStock);
        Assert.Equal(user.EmployeeId, order.Checker);
        Assert.Equal(Clock.NowValue, order.CheckedDate);
        Assert.Equal(1, store.Saves);
    }

    [Fact]
    public void ExistingDatabaseMappings_HaveExactNamesAndNoShadowForeignKeys()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var types = new[] { typeof(ManufacturingFormulaAdjustment), typeof(ManufacturingFormulaAdjustmentBatch), typeof(ManufacturingFormulaAdjustmentItem) };
        foreach (var type in types)
        {
            var entity = db.Model.FindEntityType(type)!;
            Assert.Equal("manufacturing", entity.GetSchema());
            Assert.Equal(type == typeof(ManufacturingFormulaAdjustmentBatch)
                ? "ManufacturingFormulaAdjustmentBatches" : type.Name + "s", entity.GetTableName());
            Assert.DoesNotContain(entity.GetForeignKeys().SelectMany(x => x.Properties), x => x.IsShadowProperty());
        }
        var order = db.Model.FindEntityType(typeof(MfgProductionOrder))!;
        var table = StoreObjectIdentifier.Table(order.GetTableName()!, order.GetSchema());
        Assert.Equal("is_printed_stock", order.FindProperty(nameof(MfgProductionOrder.IsPrintedStock))!.GetColumnName(table));
        Assert.False(order.FindProperty(nameof(MfgProductionOrder.IsPrintedStock))!.IsNullable);
        Assert.Equal("checker", order.FindProperty(nameof(MfgProductionOrder.Checker))!.GetColumnName(table));
        Assert.Equal("checked_date", order.FindProperty(nameof(MfgProductionOrder.CheckedDate))!.GetColumnName(table));
    }

    [Fact]
    public void QcQuantities_PreserveCumulativeReweighingAndColourDelta()
    {
        MfgAdjustmentQuantityRow[] rows =
        [new(1, 1, false, AdjustmentType.AddQuantity, 1000, "M:1", 1, null, .1m, "kg"),
         new(1, 2, false, AdjustmentType.SetQuantity, 1000, "M:1", 1, .8m, null, "kg"),
         new(1, 3, false, AdjustmentType.SetQuantity, 1000, "M:1", 1, .2m, null, "kg")];
        Assert.Equal(new[] { 1100m, 900m, 1100m }, MfgAdjustmentQuantityCalculator.CalculateTrialTotals(rows).Select(x => x.FinalQuantityGram));
        var item = Assert.Single(MfgAdjustmentQuantityCalculator.CalculateItemTotals(rows));
        Assert.Equal(1100m, item.ActualQuantityGram);
        Assert.Equal(100m, item.DeltaFromOriginalGram);
    }

    [Fact]
    public async Task List_FiltersCompany()
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<IManufacturingDbContext>();
        var user = new ProductionOrderCreationTests.TestUser();
        var order = Order(user.CompanyId!.Value);
        var other = Order(Guid.NewGuid());
        store.Seed(nameof(db.MfgProductionOrders), order, other);
        var result = await new GetProductionOrdersQueryHandler(db, user, new CurrentUserPermissionService(user))
            .Handle(new(), default);
        Assert.True(result.Success);
        Assert.Equal(order.MfgProductionOrderId, Assert.Single(result.Data!.Items).MfgProductionOrderId);
        Assert.Equal(1, result.Data.TotalCount);
    }

    private static MfgProductionOrder Order(Guid company) => new()
    {
        MfgProductionOrderId = Guid.NewGuid(), CompanyId = company, IsActive = true, UnitPriceAgreed = 123,
        Product = new Product { CompanyId = company, IsActive = true }
    };
    private sealed class Clock : IDateTimeProvider
    {
        public static readonly DateTime NowValue = new(2026, 10, 5, 12, 0, 0);
        public DateTime Now => NowValue;
    }
    private sealed class Prices(bool allowed) : IPricingVisibilityService
    {
        public PricingAccessDecision GetAccess() => new(false, allowed, false, false, false, false, false, false, false);
    }
}
