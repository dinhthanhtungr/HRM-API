using System.Reflection;
using ClosedXML.Excel;
using HRM.Application.Abstractions.Persistence.Purchasing;
using HRM.Application.Abstractions.Persistence.Reports;
using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Features.Reports.PLPU;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Application.Tests.Features.Reports.PLPU.Reference;
using HRM.Domain.Entities.AuditSchema;
using HRM.Domain.Entities.DeliverySchema;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Enums.Logs;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using HRM.Infrastructure.Documents.Excels;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.Reports.PLPU;

public sealed class PLPUReportReaderTests
{
    [Theory]
    [InlineData("BuildOrderLineQuery")]
    [InlineData("BuildReceiptQuery")]
    public void PostgreSqlQueriesTranslateWithCompanyAndLegacyFilters(string method)
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var repo = new PLPUPurchaseOverviewReportRepository((IPurchaseOrderDbContext)db);
        var query = new PLPUPurchaseOverviewQuery
        {
            CompanyId = PLPUReportParityTests.FixtureRepository.CompanyId,
            From = new(2026, 1, 1), To = new(2026, 10, 14), Keyword = "NVL",
            MaterialId = Guid.NewGuid(), SupplierId = Guid.NewGuid(), PurchaseOrderId = Guid.NewGuid(),
            OrderType = "NCC", Status = "Approved"
        };
        var result = (IQueryable)repo.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(repo, [query])!;
        var sql = result.ToQueryString();
        Assert.Contains("CompanyId", sql);
        Assert.Contains("NVL%", sql);
        Assert.Contains("%VPP%", sql);
        Assert.Contains("%BB%", sql);
        Assert.Contains("PO%", sql);
        Assert.Contains("NCC", sql);
        if (method == "BuildReceiptQuery")
        {
            Assert.Contains("deltaKg", sql);
            Assert.Contains("QCInputByQC", sql);
            Assert.True(sql.Split("CompanyId", StringSplitOptions.None).Length >= 3);
            Assert.Contains("\"companyId\"", sql);
        }
    }

    [Fact]
    public async Task FinishRowsMatchSourceAndExcludeOtherCompany()
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<IReportReadDbContext>();
        var (mfgDb, mfgStore) = ProductionOrderTestStore.CreateContext<IManufacturingDbContext>();
        var company = PLPUReportParityTests.FixtureRepository.CompanyId;
        var po = new MerchandiseOrder { MerchandiseOrderId = Guid.NewGuid(), CompanyId = company,
            ExternalId = "PO-1", CustomerNameSnapshot = "Customer", CustomerExternalIdSnapshot = "KH-1", Status = "Processing" };
        var line = new MerchandiseOrderDetail { MerchandiseOrderDetailId = Guid.NewGuid(), MerchandiseOrderId = po.MerchandiseOrderId,
            DeliveryRequestDate = new(2026, 10, 5), ExpectedQuantity = 100m, ProductExternalIdSnapshot = "PRODUCT-1" };
        var mfg = new MfgProductionOrder { MfgProductionOrderId = Guid.NewGuid(), CompanyId = company,
            ExternalId = "LSX-1", CreatedDate = new(2026, 10, 1), ExpectedDate = new(2026, 10, 6) };
        var delivery = new DeliveryOrder { Id = Guid.NewGuid(), CompanyId = company,
            ExternalId = "PGH-1", CreatedDate = new(2026, 10, 7), DeliveryAddress = "Address" };
        var detail = new DeliveryOrderDetail { Id = Guid.NewGuid(), DeliveryOrderId = delivery.Id,
            MerchandiseOrderDetailId = line.MerchandiseOrderDetailId, Quantity = 60m, LotNoList = "LOT-1" };
        store.Seed(nameof(db.MerchandiseOrders), po);
        store.Seed(nameof(db.MerchandiseOrderDetails), line);
        store.Seed(nameof(db.MfgProductionOrders), mfg);
        store.Seed(nameof(db.MfgOrderPOs), new MfgOrderPO { MerchandiseOrderDetailId = line.MerchandiseOrderDetailId,
            MfgProductionOrderId = mfg.MfgProductionOrderId, IsActive = true });
        store.Seed(nameof(db.DeliveryOrders), delivery);
        store.Seed(nameof(db.DeliveryOrderDetails), detail);
        mfgStore.Seed(nameof(mfgDb.EventLogs),
            new EventLog { EventId = Guid.NewGuid(), CompanyId = company, SourceId = mfg.MfgProductionOrderId,
                EventType = EventType.ManufacturingProductOrder, Status = "Finished", CreatedDate = new(2026, 10, 4) },
            new EventLog { EventId = Guid.NewGuid(), CompanyId = company, SourceId = mfg.MfgProductionOrderId,
                EventType = EventType.ManufacturingProductOrder, Status = "Finished", CreatedDate = new(2026, 10, 5) });
        var from = new DateTime(2026, 10, 1); var to = new DateTime(2026, 10, 14);
        var source = await new LegacyFinishReportRepository(db, mfgDb).GetFinishReportAsync(from, to, default);
        var port = new FinishPLPUReportRepository(db, mfgDb);
        var actual = await port.GetFinishReportAsync(from, to, company, default);
        PLPUReportParityTests.Equal(source, actual);
        Assert.Equal(new DateTime(2026, 10, 4), Assert.Single(actual).ProductionCompletedDate);
        Assert.Equal(60m, actual[0].DeliveredQuantity);
        Assert.Empty(await port.GetFinishReportAsync(from, to, Guid.NewGuid(), default));

        // Identical workbook cell values, styles and merged regions; binary ZIP metadata varies.
        using var expectedBook = new XLWorkbook(new MemoryStream(new LegacyFinishExcelRenderer().ExportFinishReportExcel(source)));
        using var actualBook = new XLWorkbook(new MemoryStream(new ExportFinishReportExcelHelper().ExportFinishReportExcel(actual)));
        var expectedSheet = expectedBook.Worksheet(1); var actualSheet = actualBook.Worksheet(1);
        Assert.Equal(expectedSheet.RangeUsed()!.RangeAddress.ToString(), actualSheet.RangeUsed()!.RangeAddress.ToString());
        foreach (var cell in expectedSheet.CellsUsed())
        {
            var other = actualSheet.Cell(cell.Address.RowNumber, cell.Address.ColumnNumber);
            Assert.Equal(cell.Value, other.Value);
            Assert.Equal(cell.Style, other.Style);
        }
        Assert.Equal(expectedSheet.MergedRanges.Select(x => x.RangeAddress.ToString()), actualSheet.MergedRanges.Select(x => x.RangeAddress.ToString()));
    }
}
