using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Persistence.Purchasing;
using HRM.Application.Abstractions.Persistence.Warehouse;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Purchasing.MaterialRequirements;
using HRM.Application.Features.Purchasing.MaterialRequirements.Queries.GetMaterialRequirementDemands;
using HRM.Application.Features.Purchasing.PurchaseOrders.Services;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Entities.WarehouseSchema;
using HRM.Application.Commons.Searching;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.Purchasing;

public sealed class MaterialRequirementDemandsTests
{
    [Fact]
    public void GroupingPagingAndSaleLinkFiltersTranslateToPostgreSqlWithoutDatabaseAccess()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var company = Guid.NewGuid();
        var links = MaterialRequirementSaleOrderQueries.ActiveLinks((IPLMReadDbContext)db, company);
        var saleId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var orders = MaterialRequirementDemandQueries.EligibleOrders(db.MfgProductionOrders.AsNoTracking(), company,
                new DateTime(2026, 10, 1), new DateTime(2026, 10, 31))
            .Where(x => links.Any(l => l.ProductionOrderId == x.MfgProductionOrderId && l.SaleOrderId == saleId && l.CustomerId == customerId))
            .WhereKeyword("MFG_1%", term => x => EF.Functions.ILike(x.ExternalId, term.ContainsPattern, PostgresSearchPattern.EscapeCharacter));
        var orderSql = orders.ToQueryString();
        Assert.Contains("EXISTS", orderSql);
        Assert.Contains("ILIKE", orderSql);
        var codes = new[] { "MFG001", "MFG002" };
        var demand = MaterialRequirementDemandQueries.OpenDemand(db.WarehouseTempStocks.AsNoTracking()
            .Where(x => x.CompanyId == company && x.Code.Trim().ToUpper() == "NVL001" && codes.Contains(x.VaCode.Trim().ToUpper())));
        var sql = demand.OrderBy(x => x.VaCode).Skip(15).Take(15).ToQueryString();
        Assert.Contains("GROUP BY", sql);
        Assert.Contains("HAVING", sql);
        Assert.Contains("LIMIT", sql);
        Assert.Contains("OFFSET", sql);
    }

    [Fact]
    public async Task GroupsReservationsOncePerProductionOrderAndReconcilesSummary()
    {
        var f = new Fixture();
        f.Ws.Seed(nameof(f.Warehouse.WarehouseTempStocks), f.Reserve(f.One, 1000, 200),
            f.Reserve(f.One, 30, 40), f.Reserve(f.Two, 250, 0));
        var data = (await f.Handler.Handle(f.Query(), default)).Data!;
        Assert.Equal(2, data.Rows.TotalCount);
        Assert.Equal(1050m, data.MatchedRequiredQuantityKg);
        var first = data.Rows.Items[0];
        Assert.Equal(1030m, first.RequestedQuantityKg);
        Assert.Equal(240m, first.UsedQuantityKg);
        Assert.Equal(800m, first.RequiredQuantityKg);
        Assert.Equal(f.ProductOne.ProductId, first.ProductId);
        Assert.Equal("TP001", first.ProductCode);
        Assert.Equal(f.CustomerA.CustomerId, first.CustomerId);
        Assert.Equal(2, first.SaleOrders.Count);
        var summary = new GetMaterialPurchaseRequirementsQueryHandler(f.Production, f.Warehouse, f.Purchase,
            f.User, new CurrentUserPermissionService(f.User), new PurchaseOrderReceiptReader(f.Purchase));
        var totals = await summary.Handle(new() { OnlyShortages = false }, default);
        Assert.Equal(Assert.Single(totals.Data!.Rows.Items).RequiredQuantityKg, data.MatchedRequiredQuantityKg);
    }

    [Fact]
    public async Task PaginatesLsnBeforeLoadingLinksAndKeepsFilteredTotal()
    {
        var f = new Fixture();
        var query = new GetMaterialRequirementDemandsQuery { MaterialId = f.Material.MaterialId, PageSize = 1, PageNumber = 2 };
        var data = (await f.Handler.Handle(query, default)).Data!;
        Assert.Equal(2, data.Rows.TotalCount);
        Assert.Equal(1050m, data.MatchedRequiredQuantityKg);
        var row = Assert.Single(data.Rows.Items);
        Assert.Equal(f.Two.MfgProductionOrderId, row.ProductionOrderId);
        Assert.Empty(row.SaleOrders);
        Assert.Null(row.CustomerId);
        var empty = await f.Handler.Handle(new() { MaterialId = f.Material.MaterialId, PageSize = 1, PageNumber = 3 }, default);
        Assert.Empty(empty.Data!.Rows.Items);
        Assert.Equal(2, empty.Data.Rows.TotalCount);
    }

    [Fact]
    public async Task SupportsProductProductionOrderAndInclusiveDateFilters()
    {
        var f = new Fixture();
        var data = (await f.Handler.Handle(new() { MaterialId = f.Material.MaterialId,
            ProductId = f.ProductOne.ProductId, ProductionOrderId = f.One.MfgProductionOrderId,
            FromDate = f.Date, ToDate = f.Date }, default)).Data!;
        Assert.Equal(f.One.MfgProductionOrderId, Assert.Single(data.Rows.Items).ProductionOrderId);
        Assert.Equal(800m, data.MatchedRequiredQuantityKg);
        var none = await f.Handler.Handle(new() { MaterialId = f.Material.MaterialId,
            ProductId = f.ProductOne.ProductId, ProductionOrderId = f.Two.MfgProductionOrderId }, default);
        Assert.Empty(none.Data!.Rows.Items);
        Assert.Equal(0m, none.Data.MatchedRequiredQuantityKg);
    }

    [Fact]
    public async Task CustomerAndSaleFiltersUseSameLinkAndNeverMultiplyOrAllocateQuantity()
    {
        var f = new Fixture();
        var result = await f.Handler.Handle(new() { MaterialId = f.Material.MaterialId, CustomerId = f.CustomerB.CustomerId }, default);
        Assert.Equal(f.One.MfgProductionOrderId, Assert.Single(result.Data!.Rows.Items).ProductionOrderId);
        Assert.Equal(800m, result.Data.MatchedRequiredQuantityKg);
        // MFG customer A không được dùng để ghép tùy ý với đơn hàng của customer B.
        var mismatch = await f.Handler.Handle(new() { MaterialId = f.Material.MaterialId,
            CustomerId = f.CustomerA.CustomerId, SaleOrderId = f.SaleB.MerchandiseOrderId }, default);
        Assert.Empty(mismatch.Data!.Rows.Items);
        var match = await f.Handler.Handle(new() { MaterialId = f.Material.MaterialId,
            CustomerId = f.CustomerB.CustomerId, SaleOrderId = f.SaleB.MerchandiseOrderId }, default);
        Assert.Equal(2, Assert.Single(match.Data!.Rows.Items).SaleOrders.Count);
    }

    [Fact]
    public async Task ExcludesForeignAndInactiveLinksAndReferences()
    {
        var f = new Fixture();
        f.SaleB.CompanyId = Guid.NewGuid();
        f.CustomerA.IsActive = false;
        var foreignProduct = f.ProductTwo;
        foreignProduct.CompanyId = Guid.NewGuid();
        var data = (await f.Handler.Handle(f.Query(), default)).Data!;
        var one = data.Rows.Items.Single(x => x.ProductionOrderId == f.One.MfgProductionOrderId);
        Assert.Empty(one.SaleOrders);
        Assert.Null(one.CustomerId);
        Assert.Null(one.CustomerName);
        Assert.Null(data.Rows.Items.Single(x => x.ProductionOrderId == f.Two.MfgProductionOrderId).ProductId);
        var filter = await f.Handler.Handle(new() { MaterialId = f.Material.MaterialId, SaleOrderId = f.SaleB.MerchandiseOrderId }, default);
        Assert.Empty(filter.Data!.Rows.Items);
    }

    [Fact]
    public async Task RejectsForeignMaterialAndInvalidFiltersWithoutDisclosingRecords()
    {
        var f = new Fixture();
        var missing = await f.Handler.Handle(new() { MaterialId = Guid.NewGuid() }, default);
        f.Material.CompanyId = Guid.NewGuid();
        var foreign = await f.Handler.Handle(f.Query(), default);
        Assert.False(foreign.Success);
        Assert.Equal(missing.Message, foreign.Message);
        Assert.False((await f.Handler.Handle(new() { MaterialId = Guid.Empty }, default)).Success);
        Assert.False((await f.Handler.Handle(new() { MaterialId = f.Material.MaterialId, ProductId = Guid.Empty }, default)).Success);
        Assert.False((await f.Handler.Handle(new() { MaterialId = f.Material.MaterialId, FromDate = f.Date.AddDays(1), ToDate = f.Date }, default)).Success);
    }

    [Fact]
    public async Task DeniesPurchasingWithoutProductionViewSalesAnonymousAndMissingCompany()
    {
        var f = new Fixture();
        f.User.Permissions = [ApplicationPermissions.Purchasing.ManagePurchaseOrders];
        Assert.False((await f.Handler.Handle(f.Query(), default)).Success);
        f.User.Permissions = [ApplicationPermissions.Purchasing.ManagePurchaseOrders, ApplicationPermissions.PLM.ViewProductionOrders];
        Assert.False((await f.Handler.Handle(f.Query(), default)).Success);
        f.User.HasExplicitPermissionSet = false;
        f.User.Roles = [ApplicationRoles.Sales.SaleUser];
        Assert.False((await f.Handler.Handle(f.Query(), default)).Success);
        f.User.Roles = [ApplicationRoles.President];
        Assert.True((await f.Handler.Handle(f.Query(), default)).Success);
        f.User.IsAuthenticated = false;
        Assert.False((await f.Handler.Handle(f.Query(), default)).Success);
        f.User.IsAuthenticated = true;
        f.User.CompanyId = null;
        Assert.False((await f.Handler.Handle(f.Query(), default)).Success);
    }

    [Fact]
    public async Task RejectsAmbiguousProductionCodesInsteadOfDuplicatingDemand()
    {
        var f = new Fixture();
        f.Two.ExternalId = " mfg001 ";
        Assert.False((await f.Handler.Handle(f.Query(), default)).Success);
    }

    private sealed class Fixture
    {
        public ProductionOrderCreationTests.TestUser User { get; } = new();
        public Guid Company => User.CompanyId!.Value;
        public DateTime Date { get; } = new(2026, 10, 16);
        public IPLMReadDbContext Production { get; }
        public IWarehouseReadDbContext Warehouse { get; }
        public IPurchaseOrderDbContext Purchase { get; }
        public ProductionOrderTestStore Ps { get; }
        public ProductionOrderTestStore Ws { get; }
        public ProductionOrderTestStore Ms { get; }
        public Material Material { get; }
        public MfgProductionOrder One { get; }
        public MfgProductionOrder Two { get; }
        public Product ProductOne { get; }
        public Product ProductTwo { get; }
        public Customer CustomerA { get; }
        public Customer CustomerB { get; }
        public MerchandiseOrder SaleB { get; }
        public GetMaterialRequirementDemandsQueryHandler Handler { get; }

        public Fixture()
        {
            User.HasExplicitPermissionSet = true;
            User.Permissions = [ApplicationPermissions.Purchasing.ManagePurchaseOrders,
                ApplicationPermissions.PLM.ViewProductionOrders, ApplicationPermissions.PLM.ViewFormulaMaterials];
            (Production, Ps) = ProductionOrderTestStore.CreateContext<IPLMReadDbContext>();
            (Warehouse, Ws) = ProductionOrderTestStore.CreateContext<IWarehouseReadDbContext>();
            (Purchase, Ms) = ProductionOrderTestStore.CreateContext<IPurchaseOrderDbContext>();
            Material = new() { MaterialId = Guid.NewGuid(), CompanyId = Company, ExternalId = "NVL001", Name = "NVL", IsActive = true };
            ProductOne = new() { ProductId = Guid.NewGuid(), CompanyId = Company, ColourCode = "TP001", Name = "TP A", IsActive = true };
            ProductTwo = new() { ProductId = Guid.NewGuid(), CompanyId = Company, ColourCode = "TP002", Name = "TP B", IsActive = true };
            CustomerA = new() { CustomerId = Guid.NewGuid(), CompanyId = Company, ExternalId = "KH001", CustomerName = "Khách A", IsActive = true };
            CustomerB = new() { CustomerId = Guid.NewGuid(), CompanyId = Company, ExternalId = "KH002", CustomerName = "Khách B", IsActive = true };
            One = Order("MFG001", ProductOne.ProductId, CustomerA.CustomerId);
            Two = Order("MFG002", ProductTwo.ProductId, null);
            SaleB = new() { MerchandiseOrderId = Guid.NewGuid(), ExternalId = "DHG002", CustomerId = CustomerB.CustomerId, CompanyId = Company, IsActive = true };
            var saleA = new MerchandiseOrder { MerchandiseOrderId = Guid.NewGuid(), ExternalId = "DHG001", CustomerId = CustomerA.CustomerId, CompanyId = Company, IsActive = true };
            var details = new[] { saleA, SaleB }.Select(s => new MerchandiseOrderDetail { MerchandiseOrderDetailId = Guid.NewGuid(), MerchandiseOrderId = s.MerchandiseOrderId, IsActive = true }).ToArray();
            Ps.Seed(nameof(Production.MfgProductionOrders), One, Two);
            Ps.Seed(nameof(Production.Products), ProductOne, ProductTwo);
            Ps.Seed(nameof(Production.Customers), CustomerA, CustomerB);
            Ps.Seed(nameof(Production.MerchandiseOrders), saleA, SaleB);
            Ps.Seed(nameof(Production.MerchandiseOrderDetails), details);
            Ps.Seed(nameof(Production.MfgOrderPOs), details.Select(d => new MfgOrderPO { MfgProductionOrderId = One.MfgProductionOrderId, MerchandiseOrderDetailId = d.MerchandiseOrderDetailId, IsActive = true }).ToArray());
            Ms.Seed(nameof(Purchase.Materials), Material);
            Ws.Seed(nameof(Warehouse.WarehouseTempStocks), Reserve(One, 1000, 200), Reserve(Two, 250, 0));
            Handler = new(Production, Warehouse, Purchase, User, new CurrentUserPermissionService(User));
        }

        public GetMaterialRequirementDemandsQuery Query() => new() { MaterialId = Material.MaterialId };
        private MfgProductionOrder Order(string code, Guid productId, Guid? customerId)
            => new() { MfgProductionOrderId = Guid.NewGuid(), ExternalId = code, CompanyId = Company,
                IsActive = true, Status = "Scheduled", RequiredDate = Date, ProductId = productId, CustomerId = customerId };
        public WarehouseTempStock Reserve(MfgProductionOrder order, decimal requested, decimal used)
            => new() { CompanyId = Company, VaCode = order.ExternalId, Code = " nvl001 ", ReserveStatus = "Open", QtyRequest = requested, QtyUsed = used };
    }
}
