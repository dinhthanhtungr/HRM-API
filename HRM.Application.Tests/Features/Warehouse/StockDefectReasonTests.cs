using ClosedXML.Excel;
using HRM.Application.Abstractions.Persistence.Warehouse;
using HRM.Application.Features.Warehouse.Dtos;
using HRM.Application.Features.Warehouse.Services;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.WarehouseSchema;
using HRM.Domain.Enums.WareHouses;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using HRM.Infrastructure.Documents.Excels;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Npgsql;
using System.Text.Json;

namespace HRM.Application.Tests.Features.Warehouse;

public sealed class StockDefectReasonTests
{
    [WarehouseReadOnlyFact]
    public async Task ExistingReceiptReasonsAreReadWithoutWritingOrCrossCompanyData()
    {
        using var settings = JsonDocument.Parse(Environment.GetEnvironmentVariable("HRM_WAREHOUSE_READONLY_JSON")!);
        var root = settings.RootElement;
        var cs = new NpgsqlConnectionStringBuilder
        {
            Host = root.GetProperty("host").GetString(), Port = root.GetProperty("port").GetInt32(),
            Database = root.GetProperty("database").GetString(), Username = root.GetProperty("user").GetString(),
            Password = root.GetProperty("password").GetString(),
            Options = "-c default_transaction_read_only=on -c statement_timeout=15000 -c lock_timeout=1000",
            IncludeErrorDetail = false, ApplicationName = "StockDefectReasonReadOnlyVerification"
        };
        await using var connection = new NpgsqlConnection(cs.ConnectionString);
        await connection.OpenAsync();
        await using (var guard = new NpgsqlCommand("SHOW transaction_read_only", connection))
            Assert.Equal("on", await guard.ExecuteScalarAsync());
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        var company = await db.ImportRequestFlowLogs.AsNoTracking()
            .Where(x => x.StockType == 2 && x.LogStatus == "APPLIED" && x.Note != null && x.Note.Contains("; REASON="))
            .Select(x => x.CompanyId).FirstAsync();
        var rows = await WarehouseDefectReasonReader.Query(db, company).OrderByDescending(x => x.VoucherDetailId).Take(10).ToArrayAsync();
        var sample = rows.First(x => WarehouseDefectReasonReader.ParseReason(x.Note) != null);
        var detail = new WarehouseVoucherDetailDto { VoucherDetailId = sample.VoucherDetailId };
        await WarehouseDefectReasonReader.EnrichVouchersAsync(db, company, [detail], default);
        Assert.Contains(detail.DefectReasons, x => x.Reason == WarehouseDefectReasonReader.ParseReason(sample.Note));
        var item = Item(sample.ProductCode, sample.LotNumber, StockType.DefectiveFinishedGood);
        await WarehouseDefectReasonReader.EnrichStockAsync(db, company, [item], default);
        Assert.Contains(item.StockDetailAvailables[0].DefectReasons, x => x.VoucherCode == sample.VoucherCode);
        await WarehouseDefectReasonReader.EnrichVouchersAsync(db, Guid.NewGuid(), [detail], default);
        Assert.Empty(detail.DefectReasons);
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("REASON=secret", null)]
    [InlineData("ordinary note; REASON=wrong", null)]
    [InlineData("[WIP_YIELD_CHECK] MFG=hidden; REASON=", null)]
    [InlineData("[WIP_YIELD_CHECK] MFG=hidden; REASON= SAI MÀU \r\ninternal next line", "SAI MÀU")]
    public void ParsesOnlyConfirmationReason(string? note, string? expected)
        => Assert.Equal(expected, WarehouseDefectReasonReader.ParseReason(note));

    [Fact]
    public void ComposedReasonQueryTranslatesOnPostgres()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=unused").Options);
        var sql = WarehouseDefectReasonReader.Query(db, Guid.NewGuid())
            .Where(x => new long[] { 3 }.Contains(x.VoucherDetailId)).ToQueryString();
        Assert.Contains("ProductionOutputReceiptLedger", sql);
        Assert.Contains("ImportRequestFlowLog", sql);
        Assert.Contains("VoucherDetailId", sql);
        Assert.DoesNotContain("UnitCost", sql);
    }

    [Theory]
    [InlineData("valid", true)]
    [InlineData("company", false)]
    [InlineData("lot", false)]
    [InlineData("sku", false)]
    [InlineData("cancelled", false)]
    [InlineData("pending", false)]
    [InlineData("link", false)]
    [InlineData("blankLot", false)]
    public async Task ExactReceiptBridgeScopesReasonAndDoesNotDependOnLineNumber(string mutation, bool expected)
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<IWarehouseReadDbContext>();
        var company = Guid.NewGuid();
        var flow = new ImportRequestFlowLog { CompanyId = company, RequestId = 1, RequestCode = "PRQ-1",
            RequestDetailId = 11, LineNo = 1, ProductCode = "ITEM", LotNumber = "LOT", StockType = 2,
            LogStatus = "APPLIED", AppliedQtyKg = 10, AppliedVoucherId = 2,
            Note = "[WIP_YIELD_CHECK] MFG=hidden; REASON=SAI MÀU" };
        var receipt = new ProductionOutputReceiptLedger { CompanyId = company, RequestCode = "PRQ-1", RequestDetailId = 11,
            VoucherId = 2, VoucherDetailId = 3, ProductCode = "ITEM", LotNumber = "LOT", ItemStockType = 2, QtyKg = 10 };
        switch (mutation)
        {
            case "company": flow.CompanyId = Guid.NewGuid(); break;
            case "lot": flow.LotNumber = "OTHER"; break;
            case "sku": flow.ProductCode = "OTHER"; break;
            case "cancelled": flow.CancelledAt = DateTime.UtcNow; break;
            case "pending": flow.LogStatus = "PENDING"; break;
            case "link": receipt.RequestDetailId = 12; break;
            case "blankLot": receipt.LotNumber = ""; break;
        }
        store.Seed(nameof(db.ImportRequestFlowLogs), flow);
        store.Seed(nameof(db.ProductionOutputReceiptLedgers), receipt);
        store.Seed(nameof(db.WarehouseRequests), new WarehouseRequest { CompanyId = company, RequestId = 1,
            RequestCode = "PRQ-1", ReqType = WareHouseRequestType.ImportDefectiveFinishedGood });
        store.Seed(nameof(db.WarehouseVouchers), new WarehouseVoucher { CompanyId = company, VoucherId = 2, RequestId = 1, VoucherCode = "PNK-1" });
        store.Seed(nameof(db.WarehouseVoucherDetails), new WarehouseVoucherDetail { VoucherId = 2, VoucherDetailId = 3,
            LineNo = 11000, ProductCode = "ITEM", LotNumber = "LOT", IsIncrease = true });
        var details = new[] { new WarehouseVoucherDetailDto { VoucherDetailId = 3 }, new WarehouseVoucherDetailDto { VoucherDetailId = 4 } };
        await WarehouseDefectReasonReader.EnrichVouchersAsync(db, company, details, default);
        Assert.Equal(expected ? 1 : 0, details[0].DefectReasons.Count);
        Assert.Empty(details[1].DefectReasons);
        var items = new[] { Item("ITEM", "LOT", StockType.DefectiveFinishedGood), Item("OTHER", "LOT", StockType.DefectiveFinishedGood),
            Item("ITEM", "OTHER", StockType.DefectiveFinishedGood), Item("ITEM", "LOT", StockType.FinishedGood), Item("ITEM", "", StockType.DefectiveFinishedGood) };
        await WarehouseDefectReasonReader.EnrichStockAsync(db, company, items, default);
        Assert.Equal(expected ? 1 : 0, items[0].StockDetailAvailables[0].DefectReasons.Count);
        Assert.All(items.Skip(1), item => Assert.Empty(item.StockDetailAvailables[0].DefectReasons));
        Assert.Equal(0, store.Saves);
        if (expected) Assert.Equal(new DefectReasonDto("SAI MÀU", "ImportConfirmation", "PRQ-1", "PNK-1", null), Assert.Single(details[0].DefectReasons));
    }

    [Fact]
    public void WorkbookKeepsNumericQuantitiesAllRowsAndFormulaLikeTextAsText()
    {
        var items = Enumerable.Range(0, 22).Select(i => Item("=ITEM", "LOT", StockType.DefectiveFinishedGood)).ToArray();
        items[0].StockDetailAvailables[0].DefectReasons = [new("=reason", "ImportConfirmation", "PRQ-1", "PNK-1", null)];
        var content = new StockAvailableExcelRenderer().Render(items, new(true), default);
        using var stream = new MemoryStream(content);
        using var book = new XLWorkbook(stream);
        Assert.Equal(2, book.Worksheets.Count);
        var summary = book.Worksheet("Tong hop");
        var detail = book.Worksheet("Chi tiet ke lot");
        Assert.Equal(23, summary.LastRowUsed()!.RowNumber());
        Assert.Equal(23, detail.LastRowUsed()!.RowNumber());
        Assert.False(summary.Cell(2, 1).HasFormula);
        Assert.Equal("=ITEM", summary.Cell(2, 1).GetString());
        Assert.Equal(XLDataType.Number, detail.Cell(2, 8).DataType);
        Assert.Equal(10m, detail.Cell(2, 8).GetValue<decimal>());
        Assert.Equal("Đã tắt", detail.Cell(2, 6).GetString());
        Assert.False(detail.Cell(2, 9).HasFormula);
        Assert.Equal("=reason", detail.Cell(2, 9).GetString());
        Assert.Equal("PNK-1", detail.Cell(2, 10).GetString());
    }

    private static StockAvailableDto Item(string code, string lot, StockType type) => new()
    {
        Code = code, StockType = type, PhysicalOnHandKg = 10, InactiveShelfOnHandKg = 10,
        StockDetailAvailables = [new() { LotNo = lot, ShelfStockCode = "A", OnHandKg = 10, ShelfIsActive = false }]
    };
}
