using ClosedXML.Excel;
using HRM.Application.Abstractions.Documents;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.Manufacturing.Dtos;
using HRM.Application.Features.PLM.Manufacturing.Queries.ExportProductionOrderQuantity;
using HRM.Application.Features.PLM.Shared.Authorization;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Enums.Formulas;
using HRM.Infrastructure.Documents.Excels;
using Xunit;

namespace HRM.Application.Tests.Features.PLM.Manufacturing;

public sealed class ManufacturingQuantityExportTests
{
    [Fact]
    public async Task Export_MultipliesRatioByTotalQuantityAndUsesCurrentMaterialName()
    {
        var f = new Fixture();
        var result = await f.Handler.Handle(new(" VA-ONE "), default);
        Assert.True(result.Success, result.Message);
        var row = Assert.Single(f.Renderer.Rows!);
        Assert.Equal(25m, row.RequiredQuantity);
        Assert.Equal(.25m, row.StdQuantity);
        Assert.Equal("current name", row.Detail);
        Assert.Equal("CURRENT", row.MaterialCode);
        Assert.Equal("saved product", row.ProductName);
    }

    [Theory]
    [InlineData("foreign-formula")]
    [InlineData("foreign-selection")]
    [InlineData("foreign-order")]
    [InlineData("inactive")]
    [InlineData("closed")]
    [InlineData("multiple")]
    [InlineData("zero")]
    [InlineData("visibility")]
    public async Task Export_RejectsInvalidScopeOrAmbiguousPlanBeforeRenderer(string scenario)
    {
        var f = new Fixture();
        if (scenario == "foreign-formula") f.Formula.CompanyId = Guid.NewGuid();
        if (scenario == "foreign-selection") f.Selection.CompanyId = Guid.NewGuid();
        if (scenario == "foreign-order") f.Order.CompanyId = Guid.NewGuid();
        if (scenario == "inactive") f.Order.IsActive = false;
        if (scenario == "closed") f.Selection.ValidTo = DateTime.Today;
        if (scenario == "multiple") f.Store.Seed(nameof(f.Db.ProductionSelectVersions), f.Selection, new ProductionSelectVersion {
            CompanyId = f.User.CompanyId!.Value, ManufacturingFormulaId = f.Formula.ManufacturingFormulaId,
            MfgProductionOrderId = f.Order.MfgProductionOrderId });
        if (scenario == "zero") f.Order.TotalQuantity = 0;
        if (scenario == "visibility") f.User.Roles = [ApplicationRoles.Production.ManufactureUser];
        Assert.False((await f.Handler.Handle(new("VA-ONE"), default)).Success);
        Assert.Null(f.Renderer.Rows);
    }

    [Fact]
    public void Workbook_StoresComputedNumbersAsNumbersAndUserTextAsText()
    {
        var bytes = new ProductionOrderQuantityExcelRenderer().Render([new() {
            TP = "=1+1", Formula = "VA", Detail = "NVL", StdQuantity = .25m, RequiredQuantity = 25m }]);
        using var stream = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheet("Formula Quantity");
        Assert.Equal("=1+1", sheet.Cell(2, 1).GetString());
        Assert.False(sheet.Cell(2, 1).HasFormula);
        Assert.Equal(25m, sheet.Cell(2, 8).GetValue<decimal>());
        Assert.Equal("0.0000000", sheet.Cell(2, 7).Style.NumberFormat.Format);
    }

    [Fact]
    public void LegacyXmlRenderer_ProducesSevenColumnXlsxWithRatio()
    {
        var bytes = new ManufacturingFormulaExcelRenderer().Render([new() { TP = "MFG", Formula = "VA", StdQuantity = .25m }]);
        using var stream = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheet("Formula");
        Assert.Equal(7, sheet.LastColumnUsed()!.ColumnNumber());
        Assert.Equal(.25m, sheet.Cell(2, 7).GetValue<decimal>());
        Assert.Equal("VA", sheet.Cell(2, 2).GetString());
    }

    public interface TestContext : IManufacturingDbContext, IPLMReadDbContext { }
    private sealed class Fixture
    {
        public ProductionOrderCreationTests.TestUser User { get; } = new() { Roles = [ApplicationRoles.Lab.LabUser] };
        public TestContext Db { get; }
        public ProductionOrderTestStore Store { get; }
        public MfgProductionOrder Order { get; }
        public ManufacturingFormula Formula { get; }
        public ProductionSelectVersion Selection { get; }
        public Renderer Renderer { get; } = new();
        public ExportProductionOrderQuantityQueryHandler Handler { get; }
        public Fixture()
        {
            (Db, Store) = ProductionOrderTestStore.CreateContext<TestContext>();
            var company = User.CompanyId!.Value;
            Order = new() { MfgProductionOrderId = Guid.NewGuid(), CompanyId = company, IsActive = true,
                ExternalId = "MFG", ProductNameSnapshot = "saved product", TotalQuantity = 100 };
            Formula = new() { ManufacturingFormulaId = Guid.NewGuid(), CompanyId = company, IsActive = true, ExternalId = "VA-ONE" };
            Selection = new() { MfgProductionOrderId = Order.MfgProductionOrderId, ManufacturingFormulaId = Formula.ManufacturingFormulaId,
                CompanyId = company, ValidFrom = DateTime.Today };
            var material = new Material { MaterialId = Guid.NewGuid(), CompanyId = company, Name = "current name", ExternalId = "CURRENT" };
            Store.Seed(nameof(Db.MfgProductionOrders), Order);
            Store.Seed(nameof(Db.ManufacturingFormulas), Formula);
            Store.Seed(nameof(Db.ProductionSelectVersions), Selection);
            Store.Seed(nameof(Db.Materials), material);
            Store.Seed(nameof(Db.ManufacturingFormulaMaterials), new ManufacturingFormulaMaterial {
                ManufacturingFormulaId = Formula.ManufacturingFormulaId, ManufacturingFormula = Formula, IsActive = true,
                MaterialId = material.MaterialId, Material = material, itemType = ItemType.Material, Quantity = .25m, MaterialNameSnapshot = "old" });
            var permissions = new CurrentUserPermissionService(User);
            Handler = new(Db, User, permissions, new PLMFieldVisibilityService(permissions), Renderer);
        }
    }
    private sealed class Renderer : IProductionOrderQuantityExcelRenderer
    {
        public IReadOnlyList<ProductionOrderQuantityExcelRow>? Rows;
        public byte[] Render(IReadOnlyList<ProductionOrderQuantityExcelRow> rows) { Rows = rows; return [1]; }
    }
}
