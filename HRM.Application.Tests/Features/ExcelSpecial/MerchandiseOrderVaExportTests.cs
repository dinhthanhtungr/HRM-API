using ClosedXML.Excel;
using HRM.Application.Abstractions.Documents;
using HRM.Application.Abstractions.Persistence.Dispatch;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.ExcelSpecial.Queries.ExportMerchandiseOrderVa;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.DeliverySchema;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Infrastructure.Documents.Excels;
using Xunit;

namespace HRM.Application.Tests.Features.ExcelSpecial;

public sealed class MerchandiseOrderVaExportTests
{
    [Fact]
    public async Task Export_UsesDeliveryLotsAndLegacyFallbackDeduplicatesAndKeepsEmptyOrder()
    {
        var f = new Fixture();
        Assert.True((await f.Handler.Handle(new(), default)).Success);
        Assert.Equal(new[] { "VA-1", "VA-2", "VA-3" }, f.Renderer.Rows!.Where(x => x.MerchandiseOrderExternalId == "DHG-1").Select(x => x.VaExternalId));
        Assert.Null(Assert.Single(f.Renderer.Rows!, x => x.MerchandiseOrderExternalId == "DHG-2").VaExternalId);
        Assert.All(f.Renderer.Rows!.Where(x => x.MerchandiseOrderExternalId == "DHG-1"), x => Assert.Equal("OT41059", x.ColourCode));
    }

    [Fact]
    public async Task Export_KeepsSameVaSeparateForDifferentProductsAndUsesCurrentColourCode()
    {
        var f = new Fixture();
        var second = new MerchandiseOrderDetail {
            MerchandiseOrderDetailId = Guid.NewGuid(), MerchandiseOrderId = f.Order.MerchandiseOrderId, ProductId = Guid.NewGuid(),
            ProductExternalIdSnapshot = "OLD", Product = new Product { CompanyId = f.User.CompanyId!.Value, ColourCode = "OT-SECOND" } };
        f.Store.Seed(nameof(f.Db.MerchandiseOrderDetails), f.Detail, second);
        f.Store.Seed(nameof(f.Db.DeliveryOrderDetails), f.Lines.Concat(new[] {
            new DeliveryOrderDetail { MerchandiseOrderDetailId = second.MerchandiseOrderDetailId,
                DeliveryOrder = f.Delivery, LotNoList = "VA-1" } }).ToArray());
        Assert.True((await f.Handler.Handle(new(), default)).Success);
        Assert.Equal(2, f.Renderer.Rows!.Count(x => x.VaExternalId == "VA-1"));
        Assert.Contains(f.Renderer.Rows!, x => x.ColourCode == "OT-SECOND" && x.VaExternalId == "VA-1");
    }

    [Fact]
    public async Task Export_DeduplicatesSameProductVaAcrossOrderLines()
    {
        var f = new Fixture();
        var second = new MerchandiseOrderDetail {
            MerchandiseOrderDetailId = Guid.NewGuid(), MerchandiseOrderId = f.Order.MerchandiseOrderId,
            ProductId = f.Detail.ProductId, Product = f.Detail.Product };
        f.Store.Seed(nameof(f.Db.MerchandiseOrderDetails), f.Detail, second);
        f.Store.Seed(nameof(f.Db.DeliveryOrderDetails), f.Lines.Concat(new[] {
            new DeliveryOrderDetail { MerchandiseOrderDetailId = second.MerchandiseOrderDetailId,
                DeliveryOrder = f.Delivery, LotNoList = "VA-1" } }).ToArray());
        Assert.True((await f.Handler.Handle(new(), default)).Success);
        Assert.Single(f.Renderer.Rows!, x => x.VaExternalId == "VA-1");
    }

    [Fact]
    public async Task Export_ProductWithoutDeliveryKeepsColourCodeAndSnapshotFallback()
    {
        var f = new Fixture();
        f.Detail.Product = null!;
        f.Delivery.IsActive = false;
        Assert.True((await f.Handler.Handle(new(), default)).Success);
        var row = Assert.Single(f.Renderer.Rows!, x => x.MerchandiseOrderExternalId == "DHG-1");
        Assert.Equal("OLD-CODE", row.ColourCode);
        Assert.Null(row.VaExternalId);
    }

    [Theory]
    [InlineData("foreign-delivery")]
    [InlineData("foreign-order")]
    [InlineData("cancelled")]
    [InlineData("inactive")]
    [InlineData("attachment")]
    public async Task Export_ExcludesUnrelatedOrInvalidLines(string scenario)
    {
        var f = new Fixture();
        if (scenario == "foreign-delivery") f.Delivery.CompanyId = Guid.NewGuid();
        if (scenario == "foreign-order") f.Order.CompanyId = Guid.NewGuid();
        if (scenario == "cancelled") f.Delivery.Status = "Cancelled";
        if (scenario == "inactive") f.Delivery.IsActive = false;
        if (scenario == "attachment") foreach (var line in f.Lines) line.IsAttach = true;
        Assert.True((await f.Handler.Handle(new(), default)).Success);
        Assert.DoesNotContain(f.Renderer.Rows!, x => x.VaExternalId is not null);
    }

    [Fact]
    public async Task Export_FiltersCreationDateInclusiveAndSelectedIds()
    {
        var f = new Fixture();
        Assert.True((await f.Handler.Handle(new() { From = new(2026, 10, 13), To = new(2026, 10, 13), MerchandiseOrderIds = [f.Order.MerchandiseOrderId] }, default)).Success);
        Assert.Equal(3, f.Renderer.Rows!.Count);
        Assert.True((await f.Handler.Handle(new() { From = new(2026, 10, 14) }, default)).Success);
        Assert.Empty(f.Renderer.Rows!);
    }

    [Theory]
    [InlineData("unauthenticated")]
    [InlineData("role")]
    [InlineData("company")]
    [InlineData("range")]
    [InlineData("id")]
    public async Task Export_RejectsInvalidAccessOrInputBeforeRendering(string scenario)
    {
        var f = new Fixture();
        if (scenario == "unauthenticated") f.User.IsAuthenticated = false;
        if (scenario == "role") f.User.Roles = [ApplicationRoles.Lab.LabUser];
        if (scenario == "company") f.User.CompanyId = null;
        var query = scenario == "range" ? new ExportMerchandiseOrderVaQuery { From = new(2026, 10, 14), To = new(2026, 10, 13) }
            : scenario == "id" ? new() { MerchandiseOrderIds = [Guid.Empty] } : new();
        Assert.False((await f.Handler.Handle(query, default)).Success);
        Assert.Null(f.Renderer.Rows);
    }

    [Fact]
    public void Workbook_StoresCodesAsTextAndSupportsEmptyExport()
    {
        var renderer = new MerchandiseOrderVaExcelRenderer();
        using var stream = new MemoryStream(renderer.Render([new("=1+1", "OT41059", "VA-1"), new("DHG-2", null, null)]));
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheet(1);
        Assert.False(sheet.Cell(2, 1).HasFormula);
        Assert.Equal("=1+1", sheet.Cell(2, 1).GetString());
        Assert.Equal("ColourCode", sheet.Cell(1, 2).GetString());
        Assert.Equal("OT41059", sheet.Cell(2, 2).GetString());
        Assert.Equal("VA-1", sheet.Cell(2, 3).GetString());
        Assert.True(sheet.Cell(3, 3).IsEmpty());
        Assert.NotEmpty(renderer.Render([]));
    }

    private sealed class Fixture
    {
        public ProductionOrderCreationTests.TestUser User { get; } = new();
        public MerchandiseOrder Order { get; }
        public MerchandiseOrderDetail Detail { get; }
        public IDispatchReadDbContext Db { get; }
        public ProductionOrderTestStore Store { get; }
        public DeliveryOrder Delivery { get; }
        public DeliveryOrderDetail[] Lines { get; }
        public CaptureRenderer Renderer { get; } = new();
        public ExportMerchandiseOrderVaQueryHandler Handler { get; }
        public Fixture()
        {
            var (db, store) = ProductionOrderTestStore.CreateContext<IDispatchReadDbContext>();
            Db = db;
            Store = store;
            var company = User.CompanyId!.Value;
            Order = new() { MerchandiseOrderId = Guid.NewGuid(), ExternalId = "DHG-1", CompanyId = company, CreateDate = new(2026, 10, 13, 23, 59, 59) };
            var empty = new MerchandiseOrder { MerchandiseOrderId = Guid.NewGuid(), ExternalId = "DHG-2", CompanyId = company, CreateDate = Order.CreateDate };
            var detail = new MerchandiseOrderDetail { MerchandiseOrderDetailId = Guid.NewGuid(), MerchandiseOrderId = Order.MerchandiseOrderId,
                ProductId = Guid.NewGuid(), ProductExternalIdSnapshot = "OLD-CODE", Product = new Product { CompanyId = company, ColourCode = "OT41059" } };
            Detail = detail;
            Delivery = new() { Id = Guid.NewGuid(), CompanyId = company, Status = "Completed" };
            Lines = [new() { MerchandiseOrderDetailId = detail.MerchandiseOrderDetailId, DeliveryOrder = Delivery, LotNoList = "WRONG-FALLBACK",
                LotConsumptions = [new() { LotNo = "VA-1" }, new() { LotNo = "VA-2" }, new() { LotNo = "INACTIVE", IsActive = false }] },
                new() { MerchandiseOrderDetailId = detail.MerchandiseOrderDetailId, DeliveryOrder = Delivery, LotNoList = " VA-1; VA-3 " }];
            store.Seed(nameof(db.MerchandiseOrders), Order, empty);
            store.Seed(nameof(db.MerchandiseOrderDetails), detail);
            store.Seed(nameof(db.DeliveryOrderDetails), Lines);
            Handler = new(db, User, Renderer);
        }
    }
    private sealed class CaptureRenderer : IMerchandiseOrderVaExcelRenderer
    {
        public IReadOnlyList<MerchandiseOrderVaExcelRow>? Rows;
        public byte[] Render(IReadOnlyList<MerchandiseOrderVaExcelRow> rows) { Rows = rows; return [1]; }
    }
}
