using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Persistence.Warehouse;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Purchasing.MaterialPlanning.Queries.ExpandPlanningNode;
using HRM.Application.Features.Purchasing.MaterialPlanning.Queries.GetPlanningOrders;
using HRM.Application.Features.Purchasing.MaterialPlanning.Queries.GetPlanningLines;
using HRM.Application.Features.Purchasing.MaterialPlanning.Services;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Entities.WarehouseSchema;
using HRM.Domain.Enums.Formulas;
using HRM.Domain.Enums.WareHouses;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.Purchasing;

public sealed class MaterialPlanningTests
{
    [Theory]
    [InlineData("New", true)]
    [InlineData("Approved", true)]
    [InlineData("Pending", true)]
    [InlineData("Processing", true)]
    [InlineData("Delivering", true)]
    [InlineData("Delivered", true)]
    [InlineData("Paused", true)]
    [InlineData("Completed", true)]
    [InlineData("Cancelled", true)]
    public async Task ListsAllStatusesWithoutRequiringMfg(string status, bool included)
    {
        var f = new Fixture();
        f.Order.Status = status;
        var result = await new GetPlanningOrdersQueryHandler(f.Db, f.User, f.Permissions).Handle(new(), default);
        Assert.True(result.Success);
        Assert.Equal(included ? 1 : 0, result.Data!.TotalCount);
        Assert.True((await f.Lines.Handle(new() { OrderId = f.Order.MerchandiseOrderId }, default)).Success);
        Assert.True((await f.Expand.Handle(f.Request(), default)).Success);
    }

    [Fact]
    public async Task ListAndLinesRejectForeignAndInactiveOrders()
    {
        var f = new Fixture();
        foreach (var invalid in new[] { "foreign", "inactive" })
        {
            f.Order.CompanyId = invalid == "foreign" ? Guid.NewGuid() : f.Company;
            f.Order.IsActive = invalid != "inactive";
            f.Order.Status = invalid == "closed" ? "Completed" : "New";
            Assert.Empty((await new GetPlanningOrdersQueryHandler(f.Db, f.User, f.Permissions).Handle(new(), default)).Data!.Items);
            Assert.False((await f.Lines.Handle(new() { OrderId = f.Order.MerchandiseOrderId }, default)).Success);
            Assert.False((await f.Expand.Handle(f.Request(), default)).Success);
        }
    }

    [Fact]
    public async Task MissingMfgUsesLatestProductionVaAndExpandsProductChildren()
    {
        var f = new Fixture();
        var lines = await f.Lines.Handle(new() { OrderId = f.Order.MerchandiseOrderId }, default);
        Assert.Empty(Assert.Single(lines.Data!.Items).ProductionOrders);
        var root = await f.Expand.Handle(f.Request(), default);
        Assert.True(root.Success);
        Assert.Equal("LatestProductionVa", root.Data!.Formula!.Source);
        var child = Assert.Single(root.Data.Items);
        Assert.True(child.CanExpand);
        Assert.Equal(50m, child.RequiredQuantityKg);
        var expanded = await f.Expand.Handle(f.Request([f.ProductRow.ManufacturingFormulaMaterialId]), default);
        Assert.True(expanded.Success);
        var material = Assert.Single(expanded.Data!.Items);
        Assert.Equal(10m, material.RequiredQuantityKg);
        Assert.Equal(3m, material.OnHandQuantityKg);
        Assert.False(material.CanExpand);
    }

    [Fact]
    public async Task SelectedLinkedMfgTakesPriorityOverNewerHistoryAndRejectsUnrelatedMfg()
    {
        var f = new Fixture();
        var originalFormula = f.Db.ManufacturingFormulas.First();
        var newerFormula = new ManufacturingFormula { ManufacturingFormulaId = Guid.NewGuid(),
            CompanyId = f.Company, IsActive = true, ExternalId = "VA-NEWER" };
        var newerMfg = new MfgProductionOrder { MfgProductionOrderId = Guid.NewGuid(), ProductId = f.Root.ProductId,
            CompanyId = f.Company, IsActive = true, Status = "Done" };
        var selections = f.Db.ProductionSelectVersions.ToList();
        selections.Add(new ProductionSelectVersion { ProductionSelectVersionId = Guid.NewGuid(), CompanyId = f.Company,
            MfgProductionOrderId = newerMfg.MfgProductionOrderId, MfgProductionOrder = newerMfg,
            ManufacturingFormulaId = newerFormula.ManufacturingFormulaId, ManufacturingFormula = newerFormula,
            ValidFrom = new DateTime(2026, 10, 15) });
        f.Store.Seed(nameof(f.Db.ProductionSelectVersions), selections.ToArray());
        var fallback = await PlanningFormulaReader.ReadAsync(f.Db, f.Company, f.Root.ProductId, null, default);
        Assert.Equal(newerFormula.ManufacturingFormulaId, fallback!.FormulaId);
        f.Store.Seed(nameof(f.Db.MfgOrderPOs), new MfgOrderPO { IsActive = true,
            MerchandiseOrderDetailId = f.Line.MerchandiseOrderDetailId, MfgProductionOrderId = f.Mfg.MfgProductionOrderId,
            Detail = f.Line, ProductionOrder = f.Mfg });
        var root = await f.Expand.Handle(f.Request(), default);
        Assert.Equal("SelectedMfg", root.Data!.Formula!.Source);
        Assert.Equal(originalFormula.ManufacturingFormulaId, root.Data.Formula.FormulaId);
        Assert.False((await f.Expand.Handle(new() { OrderId = f.Order.MerchandiseOrderId,
            LineId = f.Line.MerchandiseOrderDetailId, ProductionOrderId = Guid.NewGuid() }, default)).Success);
    }

    [Fact]
    public async Task MultipleMfgRequireExplicitChoiceAndDepthAndUnitsAreBounded()
    {
        var f = new Fixture();
        f.Store.Seed(nameof(f.Db.MfgOrderPOs), new MfgOrderPO { IsActive = true,
            MerchandiseOrderDetailId = f.Line.MerchandiseOrderDetailId, MfgProductionOrderId = f.Mfg.MfgProductionOrderId,
            Detail = f.Line, ProductionOrder = f.Mfg }, new MfgOrderPO { IsActive = true,
            MerchandiseOrderDetailId = f.Line.MerchandiseOrderDetailId, MfgProductionOrderId = Guid.NewGuid(),
            Detail = f.Line, ProductionOrder = f.Mfg });
        Assert.Equal("SelectProductionOrder", (await f.Expand.Handle(f.Request(), default)).Data!.Issue);
        Assert.False((await f.Expand.Handle(f.Request(Enumerable.Repeat(Guid.NewGuid(), 11).ToArray()), default)).Success);
        f.Store.Seed<MfgOrderPO>(nameof(f.Db.MfgOrderPOs));
        f.ProductRow.Unit = "bag";
        var node = Assert.Single((await f.Expand.Handle(f.Request(), default)).Data!.Items);
        Assert.Equal("UnsupportedUnit", node.Issue);
        Assert.Null(node.RequiredQuantityKg);
        Assert.False(node.CanExpand);
    }

    [Fact]
    public async Task MissingFormulaAndCycleAreExplicitAndPathCannotEscapeRoot()
    {
        var f = new Fixture();
        f.ProductRow.ProductId = f.Root.ProductId;
        var result = await f.Expand.Handle(f.Request(), default);
        Assert.Equal("CycleDetected", Assert.Single(result.Data!.Items).Issue);
        Assert.False((await f.Expand.Handle(f.Request([Guid.NewGuid()]), default)).Success);
        f.Store.Seed<ProductionSelectVersion>(nameof(f.Db.ProductionSelectVersions));
        Assert.Equal("MissingFormula", (await f.Expand.Handle(f.Request(), default)).Data!.Issue);
    }

    [Fact]
    public async Task MaterialPermissionIsRequiredAndNoPricesAreReturned()
    {
        var f = new Fixture();
        f.User.Permissions = [ApplicationPermissions.Purchasing.ManagePurchaseOrders, ApplicationPermissions.PLM.ViewProductionOrders];
        Assert.True((await f.Lines.Handle(new() { OrderId = f.Order.MerchandiseOrderId }, default)).Success);
        Assert.False((await f.Expand.Handle(f.Request(), default)).Success);
        f.User.IsAuthenticated = true;
        f.User.CompanyId = null;
        Assert.False((await f.Expand.Handle(f.Request(), default)).Success);
        var sale = new Fixture();
        sale.User.HasExplicitPermissionSet = false;
        sale.User.Roles = [ApplicationRoles.Sales.SaleUser];
        Assert.False((await new GetPlanningOrdersQueryHandler(sale.Db, sale.User, sale.Permissions).Handle(new(), default)).Success);
        f.User.IsAuthenticated = false;
        Assert.False((await new GetPlanningOrdersQueryHandler(f.Db, f.User, f.Permissions).Handle(new(), default)).Success);
        Assert.False((await f.Expand.Handle(f.Request(), default)).Success);
    }

    [Fact]
    public void OrderAndLineScopeTranslateToPostgreSql()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var sql = MaterialPlanningScope.Lines(db, Guid.NewGuid()).Select(x => new { x.ProductId, x.ExpectedQuantity }).ToQueryString();
        Assert.DoesNotContain("'New'", sql);
        Assert.DoesNotContain("'Approved'", sql);
        Assert.Contains("company", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MissingVaUsesOrderVuThenExpandsVuChildWithServerValidatedPath()
    {
        var f = new Fixture();
        f.Store.Seed<ProductionSelectVersion>(nameof(f.Db.ProductionSelectVersions));
        var (root, child, row) = f.SeedVu();
        var result = await f.Expand.Handle(f.Request(), default);
        Assert.True(result.Success);
        Assert.Equal("SelectedOrderVu", result.Data!.Formula!.Source);
        Assert.Equal(root.FormulaId, result.Data.Formula.FormulaId);
        Assert.Null(result.Data.Formula.ProductionOrderId);
        Assert.Equal(row.FormulaMaterialId, Assert.Single(result.Data.Items).SourceLineId);
        Assert.Equal(50m, result.Data.Items[0].RequiredQuantityKg);
        var expanded = await f.Expand.Handle(f.Request([row.FormulaMaterialId]), default);
        Assert.True(expanded.Success);
        Assert.Equal("LatestVu", expanded.Data!.Formula!.Source);
        Assert.Equal(child.FormulaId, expanded.Data.Formula.FormulaId);
        Assert.Equal(10m, Assert.Single(expanded.Data.Items).RequiredQuantityKg);
        Assert.False((await f.Expand.Handle(f.Request([Guid.NewGuid()]), default)).Success);
    }

    [Fact]
    public async Task VaStillWinsOverVuAndVuDoesNotEscapeCompanyProductOrActiveScope()
    {
        var f = new Fixture();
        var (root, _, _) = f.SeedVu();
        Assert.Equal("LatestProductionVa", (await f.Expand.Handle(f.Request(), default)).Data!.Formula!.Source);
        f.Store.Seed<ProductionSelectVersion>(nameof(f.Db.ProductionSelectVersions));
        foreach (var invalid in new[] { "company", "product", "inactive" })
        {
            root.CompanyId = invalid == "company" ? Guid.NewGuid() : f.Company;
            root.ProductId = invalid == "product" ? f.Child.ProductId : f.Root.ProductId;
            root.IsActive = invalid != "inactive";
            var result = await f.Expand.Handle(f.Request(), default);
            Assert.Equal("MissingFormula", result.Data!.Issue);
        }
    }

    [Fact]
    public async Task VuFallbackUsesSelectedThenNewestAndPreservesCycleDetection()
    {
        var f = new Fixture();
        f.Store.Seed<ProductionSelectVersion>(nameof(f.Db.ProductionSelectVersions));
        var (root, child, row) = f.SeedVu();
        var other = new Formula { FormulaId = Guid.NewGuid(), CompanyId = f.Company, ProductId = f.Root.ProductId,
            Product = f.Root, IsActive = true, IsSelect = true, UpdatedDate = new(2026, 10, 16), ExternalId = "VU-SELECTED" };
        f.Store.Seed(nameof(f.Db.Formulas), root, child, other);
        Assert.Equal(root.FormulaId, (await PlanningFormulaReader.ReadAsync(f.Db, f.Company,
            f.Root.ProductId, null, default, root.FormulaId))!.FormulaId);
        f.Line.FormulaId = Guid.Empty;
        Assert.Equal(other.FormulaId, (await f.Expand.Handle(f.Request(), default)).Data!.Formula!.FormulaId);
        other.IsSelect = false;
        root.UpdatedDate = new(2026, 10, 17);
        row.ProductId = f.Root.ProductId;
        var result = await f.Expand.Handle(f.Request(), default);
        Assert.Equal(root.FormulaId, result.Data!.Formula!.FormulaId);
        Assert.Equal("CycleDetected", Assert.Single(result.Data.Items).Issue);
        Assert.False(result.Data.Items[0].CanExpand);
    }

    [Theory]
    [InlineData("SelectedMfg")]
    [InlineData("LatestVu")]
    public void FormulaRowQueriesTranslateAfterPathFilteringAndPaging(string source)
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var formula = new HRM.Application.Features.Purchasing.MaterialPlanning.Dtos.PlanningFormulaDto(Guid.NewGuid(), "test", source, null);
        var query = PlanningFormulaReader.Items(db, formula);
        var childSql = query.Where(x => x.Id == Guid.NewGuid() && x.Type == ItemType.Product && x.Quantity > 0)
            .Select(x => new { x.ProductId, x.Quantity, x.Unit }).ToQueryString();
        var pageSql = query.OrderBy(x => x.LineNo).ThenBy(x => x.Id).Take(201).ToQueryString();
        Assert.Contains(source == "LatestVu" ? "FormulaMaterialId" : "ManufacturingFormulaMaterialId", childSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LIMIT", pageSql);
    }

    private sealed class Fixture
    {
        public ProductionOrderCreationTests.TestUser User { get; } = new();
        public CurrentUserPermissionService Permissions => new(User);
        public Guid Company => User.CompanyId!.Value;
        public IPLMReadDbContext Db { get; }
        public ProductionOrderTestStore Store { get; }
        public IWarehouseReadDbContext Warehouse { get; }
        public MerchandiseOrder Order { get; }
        public MerchandiseOrderDetail Line { get; }
        public Product Root { get; }
        public Product Child { get; }
        public MfgProductionOrder Mfg { get; }
        public ManufacturingFormulaMaterial ProductRow { get; }
        public GetPlanningLinesQueryHandler Lines => new(Db, Warehouse, User, Permissions);
        public ExpandPlanningNodeQueryHandler Expand => new(Db, Warehouse, User, Permissions);

        public Fixture()
        {
            User.HasExplicitPermissionSet = true;
            User.Permissions = [ApplicationPermissions.Purchasing.ManagePurchaseOrders,
                ApplicationPermissions.PLM.ViewProductionOrders, ApplicationPermissions.PLM.ViewFormulaMaterials];
            (Db, Store) = ProductionOrderTestStore.CreateContext<IPLMReadDbContext>();
            var (warehouse, ws) = ProductionOrderTestStore.CreateContext<IWarehouseReadDbContext>();
            Warehouse = warehouse;
            Order = new() { MerchandiseOrderId = Guid.NewGuid(), CompanyId = Company, Status = "New", IsActive = true };
            Root = new() { ProductId = Guid.NewGuid(), CompanyId = Company, ColourCode = "TP1", IsActive = true };
            Child = new() { ProductId = Guid.NewGuid(), CompanyId = Company, ColourCode = "TP2", IsActive = true };
            Line = new() { MerchandiseOrderDetailId = Guid.NewGuid(), MerchandiseOrderId = Order.MerchandiseOrderId,
                ProductId = Root.ProductId, ExpectedQuantity = 100, IsActive = true, MerchandiseOrder = Order };
            Mfg = new() { MfgProductionOrderId = Guid.NewGuid(), CompanyId = Company, ProductId = Root.ProductId, IsActive = true, Status = "Done" };
            var childMfg = new MfgProductionOrder { MfgProductionOrderId = Guid.NewGuid(), CompanyId = Company,
                ProductId = Child.ProductId, IsActive = true, Status = "Done" };
            var formula = new ManufacturingFormula { ManufacturingFormulaId = Guid.NewGuid(), CompanyId = Company, IsActive = true, ExternalId = "VA1" };
            var childFormula = new ManufacturingFormula { ManufacturingFormulaId = Guid.NewGuid(), CompanyId = Company, IsActive = true, ExternalId = "VA2" };
            var material = new Material { MaterialId = Guid.NewGuid(), CompanyId = Company, IsActive = true, ExternalId = "NVL1" };
            ProductRow = new() { ManufacturingFormulaMaterialId = Guid.NewGuid(), ManufacturingFormulaId = formula.ManufacturingFormulaId,
                ProductId = Child.ProductId, itemType = ItemType.Product, Quantity = 0.5m, Unit = "kg", IsActive = true };
            Store.Seed(nameof(Db.MerchandiseOrders), Order);
            Store.Seed(nameof(Db.MerchandiseOrderDetails), Line);
            Store.Seed(nameof(Db.Products), Root, Child);
            Store.Seed(nameof(Db.Materials), material);
            Store.Seed(nameof(Db.MfgProductionOrders), Mfg, childMfg);
            Store.Seed(nameof(Db.ManufacturingFormulas), formula, childFormula);
            Store.Seed(nameof(Db.ProductionSelectVersions), new[] { (Mfg, formula), (childMfg, childFormula) }.Select(pair =>
                new ProductionSelectVersion { ProductionSelectVersionId = Guid.NewGuid(), CompanyId = Company,
                    MfgProductionOrderId = pair.Item1.MfgProductionOrderId, ManufacturingFormulaId = pair.Item2.ManufacturingFormulaId,
                    MfgProductionOrder = pair.Item1, ManufacturingFormula = pair.Item2,
                    ValidFrom = new DateTime(2026, 10, 1) }).ToArray());
            Store.Seed(nameof(Db.ManufacturingFormulaMaterials), ProductRow, new ManufacturingFormulaMaterial {
                ManufacturingFormulaMaterialId = Guid.NewGuid(), ManufacturingFormulaId = childFormula.ManufacturingFormulaId,
                MaterialId = material.MaterialId, itemType = ItemType.Material, Quantity = 0.2m, Unit = "kg", IsActive = true });
            ws.Seed(nameof(Warehouse.WarehouseShelfStocks), new WarehouseShelfStock { Code = "NVL1", CompanyId = Company,
                QtyKg = 3, StockType = StockType.RawMaterial, WarehouseShelves = new() { CompanyId = Company, IsActive = true } });
        }
        public ExpandPlanningNodeQuery Request(Guid[]? path = null) => new() {
            OrderId = Order.MerchandiseOrderId, LineId = Line.MerchandiseOrderDetailId, Path = path ?? [] };

        public (Formula Root, Formula Child, FormulaMaterial Row) SeedVu()
        {
            var root = new Formula { FormulaId = Guid.NewGuid(), CompanyId = Company, ProductId = Root.ProductId,
                Product = Root, ExternalId = "VU-ROOT", IsActive = true, CreatedDate = new(2026, 10, 1) };
            var child = new Formula { FormulaId = Guid.NewGuid(), CompanyId = Company, ProductId = Child.ProductId,
                Product = Child, ExternalId = "VU-CHILD", IsActive = true, CreatedDate = new(2026, 10, 1) };
            Line.FormulaId = root.FormulaId;
            var row = new FormulaMaterial { FormulaMaterialId = Guid.NewGuid(), FormulaId = root.FormulaId,
                ProductId = Child.ProductId, itemType = ItemType.Product, Quantity = 0.5m, Unit = "kg", IsActive = true };
            Store.Seed(nameof(Db.Formulas), root, child);
            Store.Seed(nameof(Db.FormulaMaterials), row, new FormulaMaterial { FormulaMaterialId = Guid.NewGuid(),
                FormulaId = child.FormulaId, MaterialId = Db.Materials.First().MaterialId,
                itemType = ItemType.Material, Quantity = 0.2m, Unit = "kg", IsActive = true });
            return (root, child, row);
        }
    }
}
