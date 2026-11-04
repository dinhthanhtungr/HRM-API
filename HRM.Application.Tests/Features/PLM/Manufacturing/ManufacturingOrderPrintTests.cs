using HRM.Application.Abstractions.Documents;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.Manufacturing.Commands.PrintManufacturingOrder;
using HRM.Application.Features.PLM.Manufacturing.Dtos;
using HRM.Application.Features.PLM.Shared.Authorization;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Entities.WarehouseSchema;
using HRM.Domain.Enums.Manufacturings;
using HRM.Domain.Enums.WareHouses;
using HRM.Infrastructure.Documents.Pdfs;
using Xunit;

namespace HRM.Application.Tests.Features.PLM.Manufacturing;

public sealed class ManufacturingOrderPrintTests
{
    [Fact]
    public async Task Print_InfersLatestRawMaterialLotAndPersistsOnlyMissingLot()
    {
        var f = new Fixture();
        f.Row.LotNo = null;
        f.Row.MaterialExternalIdSnapshot = f.Base.Material.ExternalId;
        f.Row.Material = f.Base.Material;
        var company = f.Base.Order.CompanyId;
        f.Base.Store.Seed(nameof(f.Base.Db.WarehouseShelfStocks), f.Stock(1, "OLD", company), f.Stock(2, "NEW", company),
            f.Stock(3, "FOREIGN", Guid.NewGuid()));
        Assert.True((await f.Handler.Handle(new(f.Base.Order.MfgProductionOrderId), default)).Success);
        Assert.Equal("NEW", f.Row.LotNo);
        Assert.Equal("NEW", Assert.Single(f.Renderer.Document!.Materials).LotNo);
        Assert.Equal(100, f.Renderer.Document.Header.TotalProductionQuantity);
        Assert.Equal(1, f.Base.Store.Saves);
        Assert.True(f.Base.Store.Transaction.Committed);
        Assert.Empty(f.Base.Store.Added);
    }

    [Theory]
    [InlineData("saved")]
    [InlineData("NA")]
    public async Task Print_DoesNotOverwriteSavedLotOrNA(string reason)
    {
        var f = new Fixture();
        f.Row.LotNo = reason == "NA" ? "N/A" : "OLD";
        f.Base.Store.Seed(nameof(f.Base.Db.WarehouseShelfStocks), f.Stock(2, "NEW", f.Base.Order.CompanyId));
        Assert.True((await f.Handler.Handle(new(f.Base.Order.MfgProductionOrderId), default)).Success);
        Assert.Equal(reason == "NA" ? "N/A" : "OLD", f.Row.LotNo);
        Assert.Equal(0, f.Base.Store.Saves);
    }

    [Theory]
    [InlineData("company")]
    [InlineData("formula-company")]
    [InlineData("read-only")]
    [InlineData("renderer")]
    public async Task Print_RejectsInvalidScopeOrRollsBackRendererFailure(string reason)
    {
        var f = new Fixture();
        if (reason == "company") f.Base.Order.CompanyId = Guid.NewGuid();
        if (reason == "formula-company") f.Base.Db.ManufacturingFormulas.Single().CompanyId = Guid.NewGuid();
        if (reason == "read-only") { f.Base.User.HasExplicitPermissionSet = true;
            f.Base.User.Permissions = [ApplicationPermissions.PLM.ViewProductionOrders, ApplicationPermissions.PLM.ViewFormulaMaterials]; }
        if (reason == "renderer") f.Renderer.Throw = true;
        Assert.False((await f.Handler.Handle(new(f.Base.Order.MfgProductionOrderId), default)).Success);
        Assert.False(f.Base.Store.Transaction.Committed);
        Assert.Equal(0, f.Base.Store.Saves);
    }

    [Fact]
    public void LegacyProductionOrderTemplate_RendersCategoryAmountsAndHistory()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Evaluation;
        using var font = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "OpenSans-Regular.ttf"));
        QuestPDF.Drawing.FontManager.RegisterFont(font);
        var bytes = new ManufacturingOrderPdfRenderer().Render(new() { BatchNo = "MFG-TEST", CreatedDate = new(2026, 10, 10),
            CopiedFromCode = "VA-OLD", FormulaSelectList = "VA-HISTORY", Header = new() { FormulaExternalId = "VA-TEST",
                TotalProductionQuantity = 100, NumOfBatches = 2, ColourCode = "C", Name = "Sản phẩm", LabNote = "Kiểm tra" },
            Materials = [new() { ExternalId = "M", MaterialName = "Nguyên liệu", Quantity = 1,
                CategoryId = Guid.Parse("d5fdf825-cca1-445c-9cdb-0bae7bd0d75b"), LotNo = "L" }] });
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.True(bytes.Length > 10000);
    }

    private sealed class Fixture
    {
        public ManufacturingFormulaCreationTests.Fixture Base { get; } = new(ManufacturingFormulaOrigin.Production);
        public ManufacturingFormulaMaterial Row { get; }
        public Renderer Renderer { get; } = new();
        public PrintManufacturingOrderCommandHandler Handler { get; }
        public Fixture()
        {
            Base.User.Roles = [ApplicationRoles.Lab.LabUser];
            Base.Order.TotalQuantity = 100;
            Base.Order.NumOfBatches = 2;
            Row = Base.Db.ManufacturingFormulaMaterials.Single();
            Base.Store.Seed(nameof(Base.Db.MfgProductionOrders), Base.Order);
            Base.Store.Seed(nameof(Base.Db.ProductionSelectVersions), new ProductionSelectVersion {
                CompanyId = Base.Order.CompanyId, MfgProductionOrderId = Base.Order.MfgProductionOrderId,
                ManufacturingFormulaId = Row.ManufacturingFormulaId, ValidFrom = ManufacturingFormulaCreationTests.Fixture.Now });
            var permissions = new CurrentUserPermissionService(Base.User);
            Handler = new(Base.Db, Base.User, permissions, new PLMFieldVisibilityService(permissions), Renderer);
        }
        public WarehouseShelfStock Stock(int id, string lot, Guid company) => new() { ShelfStockId = id, Code = Base.Material.ExternalId!,
            CompanyId = company, LotNo = lot, StockType = StockType.RawMaterial, QtyKg = 1,
            UpdatedDate = ManufacturingFormulaCreationTests.Fixture.Now.AddDays(id), WarehouseShelves = new() { CompanyId = company, IsActive = true } };
    }
    private sealed class Renderer : IManufacturingOrderPdfRenderer
    {
        public ManufacturingOrderPdfDocument? Document;
        public bool Throw;
        public byte[] Render(ManufacturingOrderPdfDocument document, bool templateOnly = false)
        { if (Throw) throw new InvalidOperationException("simulated render failure"); Document = document; return [37, 80, 68, 70]; }
    }
}
