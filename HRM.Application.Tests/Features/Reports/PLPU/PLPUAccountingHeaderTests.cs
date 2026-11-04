using HRM.Application.Abstractions.Persistence.Purchasing;
using HRM.Application.Abstractions.Persistence.Reports;
using HRM.Application.Features.Reports.PLPU;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.OrderSchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.Reports.PLPU;

public sealed class PLPUAccountingHeaderTests
{
    [Fact]
    public void AccountingTotalsPreserveViewFieldsAndReconcileStatuses()
    {
        var supplier = Guid.NewGuid();
        var rows = new[] { (100m, 0L, 0m, "NO_INVOICE"), (200m, 2L, 100m, "PARTIAL"),
            (300m, 1L, 300m, "MATCHED"), (400m, 1L, 450m, "OVER") }.Select((v, i) =>
            new PLPUAccountingHeaderReader.Row
            {
                PoTotalInclVat = v.Item1 * 1.08m,
                View = new()
                {
                    SourceSupplierId = i == 0 ? null : supplier, PoStatus = "Approved", PoLineCount = 2,
                    PoQty = v.Item1 / 10m, PoAmountExclVat = v.Item1, InvoiceCount = v.Item2,
                    ApQty = v.Item3 / 10m, ApAmountExclVat = v.Item3, ApVatAmount = v.Item3 * .08m,
                    ApAmountInclVat = v.Item3 * 1.08m, DiffQty = (v.Item3 - v.Item1) / 10m,
                    DiffAmountExclVat = v.Item3 - v.Item1, ReconcileStatus = v.Item4
                }
            }).ToList();
        var result = PLPUAccountingHeaderReader.Calculate(rows);
        Assert.Equal(4, result.TotalPo); Assert.Equal(1, result.SupplierCount); Assert.Equal(1, result.PoWithoutSupplierCount);
        Assert.Equal(8, result.TotalLines); Assert.Equal(100m, result.PoQuantity);
        Assert.Equal(1000m, result.PoAmountExclVat); Assert.Equal(1080m, result.PoAmountInclVat); Assert.Equal(80m, result.PoVatAmount);
        Assert.Equal(4, result.LinkedInvoiceCount); Assert.Equal(3, result.PoWithInvoiceCount); Assert.Equal(1, result.PoWithoutInvoiceCount);
        Assert.Equal(1, result.MatchedPoCount); Assert.Equal(1, result.PartialPoCount); Assert.Equal(1, result.OverPoCount);
        Assert.Equal(850m, result.InvoiceAmountExclVat); Assert.Equal(68m, result.InvoiceVatAmount); Assert.Equal(918m, result.InvoiceAmountInclVat);
        Assert.Equal(85m, result.InvoiceQuantity); Assert.Equal(-15m, result.QuantityDifference); Assert.Equal(-150m, result.AmountDifferenceExclVat);
        Assert.Equal(100m, result.UninvoicedPoAmountExclVat); Assert.Equal(75m, result.PoWithInvoicePercent); Assert.Equal(25m, result.MatchedPoPercent);
        Assert.Equal(4, Assert.Single(result.PoStatuses).Count); Assert.False(result.IncludesInactive);
    }

    [Fact]
    public void MissingPersistedPoTotalIsNotTreatedAsZeroOrReconstructed()
    {
        var data = PLPUAccountingHeaderReader.Calculate([new() { View = new() { PoAmountExclVat = 100m } }]);
        Assert.Equal(1, data.MissingPoTotalCount); Assert.Null(data.PoVatAmount); Assert.Null(data.PoAmountInclVat);
        Assert.Equal(100m, data.PoAmountExclVat);
        var empty = PLPUAccountingHeaderReader.Calculate([]);
        Assert.Equal(0, empty.TotalPo); Assert.Equal(0m, empty.PoVatAmount); Assert.Equal(0m, empty.PoAmountInclVat);
        Assert.Equal(0, empty.PoWithInvoicePercent); Assert.Empty(empty.PoStatuses);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("PO")]
    [InlineData("NCC")]
    public async Task ReaderUsesEntireViewWithoutImplicitNvlOrPoPrefixRestrictions(string? orderType)
    {
        var (reports, rs) = ProductionOrderTestStore.CreateContext<IReportReadDbContext>();
        var (purchases, ps) = ProductionOrderTestStore.CreateContext<IPurchaseOrderDbContext>();
        var company = PLPUReportParityTests.FixtureRepository.CompanyId;
        var orders = new[] { "PO", "NCC", "KH" }.Select((type, i) => new PurchaseOrder
        {
            PurchaseOrderId = Guid.NewGuid(), CompanyId = company, ExternalId = $"{type}-001", OrderType = type,
            CreateDate = new DateTime(2026, 10, 15).AddHours(23), Status = "Approved",
            PurchaseOrderSnapshotId = Guid.NewGuid()
        }).ToArray();
        ps.Seed(nameof(purchases.PurchaseOrders), orders);
        ps.Seed(nameof(purchases.PurchaseOrderSnapshots), orders.Select(o => new PurchaseOrderSnapshot
        {
            PurchaseOrderSnapshotId = o.PurchaseOrderSnapshotId!.Value, TotalPrice = 108m
        }).ToArray());
        rs.Seed(nameof(reports.PLPUPoInvoiceSummaries), orders.Select(o => new PLPUPoInvoiceSummaryRow
        {
            CompanyId = company, PurchaseOrderId = o.PurchaseOrderId, PoNo = o.ExternalId!, PoStatus = "Approved",
            PoAmountExclVat = 100m, PoQty = 777m, InvoiceCount = 0, ReconcileStatus = "NO_INVOICE"
        }).Append(new() { CompanyId = Guid.NewGuid(), PurchaseOrderId = orders[0].PurchaseOrderId, PoAmountExclVat = 999999m }).ToArray());
        var query = new PLPUPurchaseOverviewQuery { CompanyId = company, From = new(2026, 10, 15), To = new(2026, 10, 15), OrderType = orderType, PageSize = 1 };
        var reader = new PLPUAccountingHeaderReader(reports, purchases);
        var result = await reader.ReadAsync(query, default);
        var expected = orderType == null ? 3 : 1;
        Assert.Equal(expected, result.TotalPo); Assert.Equal(expected * 100m, result.PoAmountExclVat);
        Assert.Equal(expected * 777m, result.PoQuantity); // View qty must not be replaced by request/ledger/QC quantities.
        Assert.Equal(expected * 108m, result.PoAmountInclVat);
        query.Status = "Closed";
        Assert.Equal(0, (await reader.ReadAsync(query, default)).TotalPo);
        query.Status = null; query.From = new(2026, 10, 16); query.To = new(2026, 10, 16);
        Assert.Equal(0, (await reader.ReadAsync(query, default)).TotalPo);
    }

    [Fact]
    public void AccountingViewMappingIsKeylessAndQueryTranslatesWithAllFilters()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var model = db.Model.FindEntityType(typeof(PLPUPoInvoiceSummaryRow))!;
        Assert.Null(model.FindPrimaryKey()); Assert.Equal("purchase", model.GetViewSchema()); Assert.Equal("v_po_ap_invoice_summary", model.GetViewName());
        var reader = new PLPUAccountingHeaderReader((IReportReadDbContext)db, (IPurchaseOrderDbContext)db);
        var sql = reader.Query(new()
        {
            CompanyId = Guid.NewGuid(), From = new(2026, 1, 1), To = new(2026, 12, 31),
            Keyword = "PO", SupplierId = Guid.NewGuid(), MaterialId = Guid.NewGuid(), Status = "Approved", OrderType = "PO"
        }).ToQueryString();
        Assert.Contains("purchase.v_po_ap_invoice_summary", sql);
        Assert.Contains("company_id", sql); Assert.Contains("CompanyId", sql);
        Assert.Contains("PurchaseOrderSnapshot", sql); Assert.Contains("CreateDate", sql);
        Assert.DoesNotContain("NVL%", sql); Assert.DoesNotContain("%VPP%", sql);
        Assert.Throws<InvalidOperationException>(() => reader.Query(new()));
    }
}
