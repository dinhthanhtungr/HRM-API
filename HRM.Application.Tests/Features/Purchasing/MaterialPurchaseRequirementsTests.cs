using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Persistence.Purchasing;
using HRM.Application.Abstractions.Persistence.Warehouse;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Purchasing.MaterialRequirements;
using HRM.Application.Features.Purchasing.PurchaseOrders.Services;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Entities.WarehouseSchema;
using HRM.Domain.Enums.WareHouses;

namespace HRM.Application.Tests.Features.Purchasing;

public sealed class MaterialPurchaseRequirementsTests
{
    [Theory]
    [InlineData("Scheduled", true)]
    [InlineData("Scheduling", true)]
    [InlineData("Waiting", true)]
    [InlineData("Started", false)]
    [InlineData("Unscheduled", false)]
    [InlineData("Completed", false)]
    [InlineData("Cancelled", false)]
    [InlineData("InProgress", false)]
    [InlineData("New", false)]
    [InlineData("FormulaRequested", false)]
    [InlineData("FormulaSuccess", false)]
    [InlineData("Canceled", false)]
    [InlineData("Finished", false)]
    [InlineData("Done", false)]
    [InlineData("Stocked", false)]
    [InlineData("Unknown", false)]
    [InlineData(null, false)]
    public async Task OnlyPlanningStatusesContributeDemandAndMissingReservationWarning(string? status, bool eligible)
    {
        var f = new Fixture();
        var order = f.Order();
        order.Status = status!;
        var missing = f.Order("MISSING");
        missing.Status = status!;
        f.Os.Seed(nameof(f.Production.MfgProductionOrders), order, missing);

        var result = await f.Handler.Handle(new(), default);

        Assert.True(result.Success);
        Assert.Equal(eligible ? 1 : 0, result.Data!.Rows.TotalCount);
        Assert.Equal(eligible ? 1 : 0, result.Data.ProductionOrdersWithoutReservations);
        Assert.Equal(!eligible, result.Data.IsDemandComplete);
    }

    [Fact]
    public async Task ReconcilesScreenshotAndSubtractsReceiptOnceForRepeatedPoMaterial()
    {
        var f = new Fixture();
        f.Ps.Seed(nameof(f.Purchase.PurchaseOrderDetails), f.Line(1000), f.Line(1000));
        f.Ps.Seed(nameof(f.Purchase.WarehouseRequests), new WarehouseRequest
            { RequestId = 1, CompanyId = f.Company, IsActive = true, codeFromRequest = "PO1" });
        f.Ps.Seed(nameof(f.Purchase.WarehouseVouchers), new WarehouseVoucher
            { VoucherId = 1, RequestId = 1, CompanyId = f.Company, VoucherType = (int)WareHouseRequestType.ImportRawMaterial });
        f.Ps.Seed(nameof(f.Purchase.WarehouseVoucherDetails), new WarehouseVoucherDetail
            { VoucherDetailId = 1, VoucherId = 1, ProductCode = "NVL_NH_466" });
        f.Ps.Seed(nameof(f.Purchase.WarehouseShelfLedgers), new WarehouseShelfLedger
            { CompanyId = f.Company, VoucherDetailId = 1, DeltaKg = 450 });

        var result = await f.Handler.Handle(new(), default);
        Assert.True(result.Success);
        var row = Assert.Single(result.Data!.Rows.Items);
        Assert.Equal(5010m, row.RequiredQuantityKg);
        Assert.Equal(117.5m, row.OnHandQuantityKg);
        Assert.Equal(1550m, row.PendingPurchaseQuantityKg);
        Assert.Equal(3342.5m, row.PurchaseQuantityKg);
        Assert.Equal("Shortage", row.Status);
        Assert.Equal(f.Date, row.RequiredBeforeDate);
        Assert.True(result.Data.IsDemandComplete);
    }

    [Fact]
    public async Task ExcludesForeignInactiveTerminalAndConsumedDemandAndReportsMissingReservations()
    {
        var f = new Fixture();
        f.Os.Seed(nameof(f.Production.MfgProductionOrders), f.Order(),
            f.Order("FOREIGN", Guid.NewGuid()), f.Order("INACTIVE", active: false),
            f.Order("DONE", status: "Done"), f.Order("MISSING"), f.Order("USED"));
        f.Ws.Seed(nameof(f.Warehouse.WarehouseTempStocks), f.Reserve(),
            f.Reserve("FOREIGN", company: Guid.NewGuid()), f.Reserve("INACTIVE"), f.Reserve("DONE"),
            f.Reserve("USED", 500, 500), f.Reserve(quantity: 9999, state: "Cancelled"));
        var result = await f.Handler.Handle(new(), default);
        Assert.Equal(5010m, Assert.Single(result.Data!.Rows.Items).RequiredQuantityKg);
        Assert.Equal(1, result.Data.ProductionOrdersWithoutReservations);
        Assert.False(result.Data.IsDemandComplete);
    }

    [Fact]
    public async Task UsesRemainingDemandAndExcludesDefectiveAndInactiveShelfStock()
    {
        var f = new Fixture();
        f.Ws.Seed(nameof(f.Warehouse.WarehouseTempStocks), f.Reserve(quantity: 1000, used: 200));
        f.Ws.Seed(nameof(f.Warehouse.WarehouseShelfStocks), f.Stock(100),
            f.Stock(5000, StockType.DefectiveRawMaterial), f.Stock(5000, active: false),
            f.Stock(5000, company: Guid.NewGuid()));
        f.Ps.Seed(nameof(f.Purchase.PurchaseOrders), f.Po("Completed"));
        var result = await f.Handler.Handle(new(), default);
        var row = Assert.Single(result.Data!.Rows.Items);
        Assert.Equal(800m, row.RequiredQuantityKg);
        Assert.Equal(100m, row.OnHandQuantityKg);
        Assert.Equal(0m, row.PendingPurchaseQuantityKg);
        Assert.Equal(700m, row.PurchaseQuantityKg);
    }

    [Fact]
    public async Task FiltersDateAndShortageBeforePagination()
    {
        var f = new Fixture();
        var outside = await f.Handler.Handle(new() { FromDate = f.Date.AddDays(1) }, default);
        Assert.Empty(outside.Data!.Rows.Items);
        f.Ws.Seed(nameof(f.Warehouse.WarehouseShelfStocks), f.Stock(10000));
        Assert.Empty((await f.Handler.Handle(new(), default)).Data!.Rows.Items);
        var all = await f.Handler.Handle(new() { OnlyShortages = false, FromDate = f.Date, ToDate = f.Date }, default);
        Assert.Equal("Sufficient", Assert.Single(all.Data!.Rows.Items).Status);
        Assert.Equal(0m, all.Data.Rows.Items[0].PurchaseQuantityKg);
        var page = await f.Handler.Handle(new() { OnlyShortages = false, PageNumber = 2, PageSize = 1 }, default);
        Assert.Empty(page.Data!.Rows.Items);
        Assert.Equal(1, page.Data.Rows.TotalCount);
        Assert.False((await f.Handler.Handle(new() { FromDate = f.Date.AddDays(1), ToDate = f.Date }, default)).Success);
    }

    [Fact]
    public async Task NormalizesMaterialAndOrderCodesWithoutDuplicatingDemand()
    {
        var f = new Fixture();
        var reserve = f.Reserve(" mfg1 ");
        reserve.Code = " nvl_nh_466 ";
        f.Ws.Seed(nameof(f.Warehouse.WarehouseTempStocks), reserve);
        var stock = f.Stock(100);
        stock.Code = "nvl_nh_466";
        f.Ws.Seed(nameof(f.Warehouse.WarehouseShelfStocks), stock);
        var duplicate = new Material { MaterialId = Guid.NewGuid(), ExternalId = "nvl_nh_466", CompanyId = f.Company, IsActive = true };
        f.Ps.Seed(nameof(f.Purchase.Materials), f.Material, duplicate);
        var result = await f.Handler.Handle(new(), default);
        Assert.True(result.Data!.IsDemandComplete);
        var row = Assert.Single(result.Data.Rows.Items);
        Assert.Equal(100m, row.OnHandQuantityKg);
        Assert.Equal(5010m, row.RequiredQuantityKg);
        Assert.Equal("NVL_NH_466", row.MaterialCode);
    }

    [Theory]
    [InlineData(100, 150, 0)]
    [InlineData(100, 40, 60)]
    public void ClampsOverReceiptAtEachPurchaseOrder(decimal requested, decimal received, decimal expected)
        => Assert.Equal(expected, MaterialPurchaseRequirementRules.Remaining(requested, received));

    [Fact]
    public async Task DeniesSalesUnauthenticatedAndMissingCompany()
    {
        var f = new Fixture();
        f.User.Roles = [ApplicationRoles.Sales.SaleUser];
        Assert.False((await f.Handler.Handle(new(), default)).Success);
        f.User.Roles = [ApplicationRoles.Purchasing.Purchaser];
        f.User.IsAuthenticated = false;
        Assert.False((await f.Handler.Handle(new(), default)).Success);
        f.User.IsAuthenticated = true;
        f.User.CompanyId = null;
        Assert.False((await f.Handler.Handle(new(), default)).Success);
    }

    private sealed class Fixture
    {
        public ProductionOrderCreationTests.TestUser User { get; } = new();
        public Guid Company => User.CompanyId!.Value;
        public DateTime Date { get; } = new(2026, 9, 29);
        public IPLMReadDbContext Production { get; }
        public IWarehouseReadDbContext Warehouse { get; }
        public IPurchaseOrderDbContext Purchase { get; }
        public ProductionOrderTestStore Os { get; }
        public ProductionOrderTestStore Ws { get; }
        public ProductionOrderTestStore Ps { get; }
        public Material Material { get; }
        public GetMaterialPurchaseRequirementsQueryHandler Handler { get; }

        public Fixture()
        {
            User.Roles = [ApplicationRoles.Purchasing.Purchaser];
            (Production, Os) = ProductionOrderTestStore.CreateContext<IPLMReadDbContext>();
            (Warehouse, Ws) = ProductionOrderTestStore.CreateContext<IWarehouseReadDbContext>();
            (Purchase, Ps) = ProductionOrderTestStore.CreateContext<IPurchaseOrderDbContext>();
            Material = new() { MaterialId = Guid.NewGuid(), CompanyId = Company, IsActive = true, ExternalId = "NVL_NH_466", Name = "Hạt nhựa PE đen" };
            Os.Seed(nameof(Production.MfgProductionOrders), Order());
            Ws.Seed(nameof(Warehouse.WarehouseTempStocks), Reserve());
            Ws.Seed(nameof(Warehouse.WarehouseShelfStocks), Stock(117.5m));
            Ps.Seed(nameof(Purchase.Materials), Material);
            Ps.Seed(nameof(Purchase.PurchaseOrders), Po());
            Ps.Seed(nameof(Purchase.PurchaseOrderDetails), Line(1550));
            Handler = new(Production, Warehouse, Purchase, User, new CurrentUserPermissionService(User), new PurchaseOrderReceiptReader(Purchase));
        }

        public MfgProductionOrder Order(string code = "MFG1", Guid? company = null, bool active = true, string status = "Scheduled")
            => new() { ExternalId = code, CompanyId = company ?? Company, IsActive = active, Status = status, RequiredDate = Date };
        public WarehouseTempStock Reserve(string order = "MFG1", decimal quantity = 5010, decimal used = 0, Guid? company = null, string state = "Open")
            => new() { VaCode = order, Code = "NVL_NH_466", CompanyId = company ?? Company, QtyRequest = quantity, QtyUsed = used, ReserveStatus = state };
        public WarehouseShelfStock Stock(decimal quantity, StockType type = StockType.RawMaterial, bool active = true, Guid? company = null)
            => new() { Code = "NVL_NH_466", CompanyId = company ?? Company, QtyKg = quantity, StockType = type, WarehouseShelves = new() { IsActive = active } };
        public PurchaseOrder Po(string status = "InProgress")
            => new() { PurchaseOrderId = Material.MaterialId, CompanyId = Company, ExternalId = "PO1", Status = status, IsActive = true };
        public PurchaseOrderDetail Line(decimal quantity)
            => new() { PurchaseOrderId = Material.MaterialId, Material = Material, MaterialId = Material.MaterialId, MaterialExternalIDSnapshot = Material.ExternalId, RequestQuantity = quantity, IsActive = true };
    }
}
