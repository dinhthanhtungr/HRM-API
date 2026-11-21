using System.Reflection;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.PLM.Manufacturing.Queries.ResolveProductionExcel;
using HRM.Application.Features.PLM.Shared.Authorization;
using HRM.Application.Features.Warehouse.Dtos;
using HRM.Application.Features.Warehouse.Queries.GetStockAvailable;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Enums.WareHouses;
using MediatR;

namespace HRM.Application.Tests.Features.PLM.Manufacturing;

public sealed class ProductionExcelTests
{
    [Theory]
    [InlineData("LabUser")]
    [InlineData("President")]
    [InlineData("PLPUUser")]
    [InlineData("Admin")]
    [InlineData("anonymous")]
    [InlineData("explicit-deny")]
    public async Task RejectsNonDeveloperBeforeDatabaseAccess(string scenario)
    {
        var user = new ProductionOrderCreationTests.TestUser { Roles = [scenario] };
        if (scenario == "anonymous") { user.Roles = ["Developer"]; user.IsAuthenticated = false; }
        if (scenario == "explicit-deny") { user.Roles = ["Developer"]; user.HasExplicitPermissionSet = true; }
        var permission = new CurrentUserPermissionService(user);
        var handler = new ResolveProductionExcelQueryHandler(null!, null!, user, permission, new PLMFieldVisibilityService(permission), null!);
        Assert.False((await handler.Handle(new(Guid.NewGuid(), [Row]), default)).Success);
    }

    [Theory]
    [InlineData("foreign-company")]
    [InlineData("inactive")]
    [InlineData("Scheduling")]
    [InlineData("WaitingForStandardSample")]
    [InlineData("no-link")]
    public async Task RejectsInvalidScopeStateOrQuantityWithoutWarehouseRead(string scenario)
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<IManufacturingDbContext>();
        var user = new ProductionOrderCreationTests.TestUser { Roles = ["Developer"] };
        var order = new MfgProductionOrder { MfgProductionOrderId = Guid.NewGuid(), CompanyId = user.CompanyId!.Value, IsActive = true, Status = "New" };
        if (scenario == "foreign-company") order.CompanyId = Guid.NewGuid();
        if (scenario == "inactive") order.IsActive = false;
        if (scenario is "Scheduling" or "WaitingForStandardSample") order.Status = scenario;
        store.Seed("MfgProductionOrders", order);
        var permission = new CurrentUserPermissionService(user);
        var handler = new ResolveProductionExcelQueryHandler(db, null!, user, permission, new PLMFieldVisibilityService(permission), null!);
        Assert.False((await handler.Handle(new(order.MfgProductionOrderId, [Row]), default)).Success);
        Assert.Equal(0, store.Saves);
    }

    [Fact]
    public async Task DeveloperSumsAllUsableShelvesByLotAndQualityWithoutSaving()
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<IManufacturingDbContext>();
        var (read, readStore) = ProductionOrderTestStore.CreateContext<IPLMReadDbContext>();
        var user = new ProductionOrderCreationTests.TestUser { Roles = ["Developer"] };
        var order = new MfgProductionOrder { MfgProductionOrderId = Guid.NewGuid(), CompanyId = user.CompanyId!.Value, IsActive = true, Status = "FormulaRequested" };
        store.Seed("MfgProductionOrders", order);
        store.Seed("MfgOrderPOs", new MfgOrderPO { MfgProductionOrderId = order.MfgProductionOrderId, IsActive = true,
            Detail = new() { IsActive = true, MerchandiseOrder = new MerchandiseOrder { IsActive = true, CompanyId = order.CompanyId } } });
        var product = new HRM.Domain.Entities.SampleRequestSchema.Product { ProductId = Guid.NewGuid(), CompanyId = order.CompanyId, IsActive = true,
            ColourCode = "BLUE", Name = "Canonical", CategoryId = Guid.NewGuid(), Unit = "kg" };
        store.Seed("Products", product);
        readStore.Seed("Products", product);
        var sender = DispatchProxy.Create<ISender, StockSender>();
        var permission = new CurrentUserPermissionService(user);
        var handler = new ResolveProductionExcelQueryHandler(db, read, user, permission, new PLMFieldVisibilityService(permission), sender);
        var result = await handler.Handle(new(order.MfgProductionOrderId, [Row, Row with { LotNo = "MISSING" }, Row]), default);
        Assert.True(result.Success, result.Message);
        var item = result.Data![0].Item!;
        Assert.Equal(1000m, item.StockKg);
        Assert.Equal(0m, item.Percent);
        Assert.Equal(3, item.ItemType);
        Assert.Equal(2, item.StockType);
        Assert.Equal("[BLUE] Canonical", item.Name);
        Assert.Equal("NoStock", result.Data[1].Error);
        Assert.Equal("DuplicateItemLot", result.Data[2].Error);
        var valid = await handler.Handle(new(order.MfgProductionOrderId, [Row, Row with { LotNo = "OTHER" }]), default);
        Assert.True(valid.Success, valid.Message);
        Assert.Equal(decimal.Truncate(1000m / 1900m * 100 * 10000000000m) / 10000000000m, valid.Data![0].Item!.Percent);
        Assert.True(valid.Data[1].Item!.Percent > 0);
        Assert.Equal(100m, valid.Data.Sum(x => x.Item!.Percent));
        var single = await handler.Handle(new(order.MfgProductionOrderId, [Row]), default);
        Assert.Equal(100m, single.Data![0].Item!.Percent);
        Assert.Equal(0, store.Saves);
        Assert.Empty(store.Added);
    }

    private static ProductionExcelRow Row => new("BLUE", "Untrusted Excel name", "VA1", "TP lỗi");
    public class StockSender : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            var query = Assert.IsType<GetStockAvailableQuery>(args![0]);
            Assert.Equal(new[] { "BLUE" }, query.ExactCodes!);
            var bad = new StockAvailableDto { Code = "BLUE", StockType = StockType.DefectiveFinishedGood };
            bad.StockDetailAvailables.AddRange([
                new() { ShelfStockCode = "M.0.7", LotNo = "VA1", CanUse = true, OnHandKg = 100 },
                new() { ShelfStockCode = "W.0.3", LotNo = "VA1", CanUse = true, OnHandKg = 900 },
                new() { ShelfStockCode = "M.0.7", LotNo = "VA1", CanUse = false, OnHandKg = 900 },
                new() { ShelfStockCode = "M.0.7", LotNo = "OTHER", CanUse = true, OnHandKg = 900 }]);
            var good = new StockAvailableDto { Code = "BLUE", StockType = StockType.FinishedGood };
            good.StockDetailAvailables.Add(new() { ShelfStockCode = "M.0.7", LotNo = "VA1", CanUse = true, OnHandKg = 999 });
            return Task.FromResult(OperationResult<PagedResult<StockAvailableDto>>.Ok(new PagedResult<StockAvailableDto>([bad, good], 2, 1, 2)));
        }
    }
}
