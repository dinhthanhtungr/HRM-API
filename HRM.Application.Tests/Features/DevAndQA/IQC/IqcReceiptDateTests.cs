using HRM.Application.Abstractions.Persistence.DevAndQA;
using HRM.Application.Features.DevAndQA.IQC.Dtos;
using HRM.Application.Features.DevAndQA.IQC.Services;
using HRM.Domain.Entities.DevandqaSchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Entities.WarehouseSchema;
using HRM.Domain.Enums.WareHouses;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.DevAndQA.IQC;

public sealed class IqcReceiptDateTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SummaryMeasuresOnlyExecutedPhases(bool includeLabTests)
    {
        var reader = Fixture(withTests: true);
        using var timing = new HRM.Application.Commons.Diagnostics.RequestTiming();
        await reader.GetPagedSummaryAsync(new() { IncludeLabTests = includeLabTests }, default);
        var phases = timing.Snapshot().ToDictionary();
        Assert.Contains("iqc_count", phases.Keys);
        Assert.Contains("iqc_page_keys", phases.Keys);
        Assert.Contains("iqc_warehouse_rows", phases.Keys);
        Assert.Contains("iqc_map", phases.Keys);
        Assert.Equal(includeLabTests, phases.ContainsKey("iqc_lab_rows"));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(null, true)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public void SuggestionMetadataKeepsTheSameVisibleRowsAndValuesAsTheList(bool? hasQc, bool dateFilter)
    {
        var reader = Fixture(withTests: true);
        var request = new QCInputQuery {
            IncludeLabTests = true, HasQC = hasQc,
            FromDate = dateFilter ? new(2026, 10, 26) : null,
            ToDate = dateFilter ? new(2026, 10, 27, 23, 59, 59) : null
        };
        var list = reader.BuildSmartRows(request)
            .Select(x => new { x.VoucherDetailId, x.LabTestId, x.Code, x.Name, x.LotNumber }).ToHashSet();
        request.SuggestionKeyword = "MAT";
        var suggestions = reader.BuildSmartRows(request)
            .Select(x => new { x.VoucherDetailId, x.LabTestId, x.Code, x.Name, x.LotNumber }).ToHashSet();
        Assert.True(list.SetEquals(suggestions));
    }

    [Fact]
    public async Task ReceiptDateUsesLatestScopedLedgerPerLineNotHeaderOrQcDate()
    {
        var reader = Fixture();
        var page = await reader.GetPagedSummaryAsync(new QCInputQuery(), default);
        Assert.Equal(3, page.TotalCount);
        var line = Assert.Single(page.Items, x => x.VoucherDetailId == 34302);
        Assert.Equal(new DateTime(2026, 10, 26, 13, 55, 34), line.InputDate);
        Assert.Equal(new DateTime(2026, 10, 26, 14, 48, 12), line.QCCreatedDate);
        Assert.Equal(new DateTime(2026, 10, 25, 9, 0, 0), page.Items.Single(x => x.VoucherDetailId == 34226).InputDate);
        Assert.Null(page.Items.Single(x => x.VoucherDetailId == 34303).InputDate);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DateFilterRunsBeforePaginationAndMatchesBothExcelSheets(bool qcFilter)
    {
        var reader = Fixture();
        var filter = new QCInputQuery { FromDate = new(2026, 10, 26), ToDate = new(2026, 10, 26, 23, 59, 59),
            HasQC = qcFilter ? true : null, PageSize = 1 };
        var page = await reader.GetPagedSummaryAsync(filter, default);
        Assert.Equal(1, page.TotalCount);
        var row = Assert.Single(page.Items);
        Assert.Equal(34302, row.VoucherDetailId);
        var export = Assert.Single(await reader.GetExportRowsAsync(filter, default));
        Assert.Equal(row.InputDate, export.CreatedDate);
        Assert.Equal(row.QCCreatedDate, export.InspectionDate);
        var summary = Assert.Single(await reader.GetSummaryRowsForExportAsync(filter, default));
        Assert.Equal(row.InputDate, summary.InputDate);
        filter.FromDate = new(2026, 9, 1);
        filter.ToDate = new(2026, 9, 30, 23, 59, 59);
        Assert.Empty(await reader.BuildSummaryIdsQuery(filter).ToListAsync());
        Assert.Empty(await reader.GetExportRowsAsync(filter, default));
    }

    [Fact]
    public async Task ExactTimestampBoundsAreInclusiveAndUnpostedRowsDoNotFallbackToHeaderDate()
    {
        var reader = Fixture();
        var instant = new DateTime(2026, 10, 26, 13, 55, 34);
        var ids = await reader.BuildSummaryIdsQuery(new QCInputQuery { FromDate = instant, ToDate = instant }).ToListAsync();
        Assert.Equal(new long[] { 34302 }, ids);
        Assert.Empty(await reader.BuildSummaryIdsQuery(new QCInputQuery { VoucherDetailId = 34303, FromDate = new(2026, 1, 1) }).ToListAsync());
    }

    [Fact]
    public async Task CombinedSummaryPagesBothSourcesAndScopesTestSamples()
    {
        var reader = Fixture(withTests: true);
        var legacy = await reader.GetPagedSummaryAsync(new(), default);
        Assert.Equal(3, legacy.TotalCount);
        var filter = new QCInputQuery { IncludeLabTests = true, PageSize = 1 };
        var first = await reader.GetPagedSummaryAsync(filter, default);
        Assert.Equal(5, first.TotalCount);
        var sample = Assert.Single(first.Items);
        Assert.Equal("LabTest", sample.Source);
        Assert.Equal("NVL_TEST_00002", sample.ExternalId);
        Assert.Equal(0, sample.VoucherDetailId);
        Assert.Null(sample.QCInputByQCId);
        Assert.Null(sample.InputDate);
        Assert.Equal(new DateOnly(2026, 10, 27), sample.TestDate);
        Assert.Equal("LOT-TEST", sample.LotNumber);
        Assert.Equal(2.5m, sample.WeightKg);
        Assert.Equal("kg", sample.Unit);
        filter.PageNumber = 2;
        Assert.Equal("NVL_TEST_00001", Assert.Single((await reader.GetPagedSummaryAsync(filter, default)).Items).ExternalId);
        filter.PageNumber = 3;
        Assert.Equal(34302, Assert.Single((await reader.GetPagedSummaryAsync(filter, default)).Items).VoucherDetailId);
        filter.PageNumber = 1;
        filter.FromDate = new(2026, 10, 27);
        filter.ToDate = new(2026, 10, 27, 23, 59, 59);
        Assert.Equal(1, (await reader.GetPagedSummaryAsync(filter, default)).TotalCount);
        filter.FromDate = null; filter.ToDate = null;
        filter.VoucherDetailId = 34302;
        Assert.Equal(1, (await reader.GetPagedSummaryAsync(filter, default)).TotalCount);
        filter.VoucherDetailId = 0;
        filter.VoucherDetailType = VoucherDetailType.QCPass;
        Assert.Equal(3, (await reader.GetPagedSummaryAsync(filter, default)).TotalCount);
        filter.VoucherDetailType = null;
        filter.HasQC = false;
        Assert.Empty((await reader.GetPagedSummaryAsync(filter, default)).Items);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CombinedKeyQueryTranslatesToPostgresBeforePaging(bool filtered)
    {
        using var db = new HRM.Infrastructure.DatabaseContext.ApplicationDbs.ApplicationDbContext(
            new DbContextOptionsBuilder<HRM.Infrastructure.DatabaseContext.ApplicationDbs.ApplicationDbContext>().UseNpgsql().Options);
        var sql = new IqcReader(db, Guid.NewGuid()).BuildCombinedSummaryKeys(new QCInputQuery
            { Keyword = filtered ? "NVL_TEST" : null, FromDate = filtered ? new DateTime(2026, 10, 1) : null })
            .OrderByDescending(x => x.SortDate).ThenByDescending(x => x.VoucherDetailId).ThenByDescending(x => x.LabTestId)
            .Skip(15).Take(15).ToQueryString();
        Assert.Contains("UNION ALL", sql);
        Assert.Contains("LIMIT", sql);
        Assert.Contains("OFFSET", sql);
        if (filtered) Assert.Contains("ILIKE", sql);
        Assert.Contains("GROUP BY", sql);
        Assert.Contains("LEFT JOIN", sql);
        // Regression: sorting must not evaluate a scalar MAX subquery for every warehouse candidate.
        Assert.DoesNotMatch(@"(?is)COALESCE\(\s*\(\s*SELECT\s+max\(", sql);
    }

    private static IqcReader Fixture(bool withTests = false)
    {
        var (db, store) = IqcTestStore.CreateStore();
        var company = Guid.NewGuid(); var employee = Guid.NewGuid(); var category = Guid.NewGuid();
        var header = new WarehouseVoucher { VoucherId = 13328, CompanyId = company,
            VoucherType = (int)WareHouseRequestType.ImportOther, CreatedDate = new(2026, 9, 26, 8, 36, 42) };
        WarehouseVoucherDetail Line(long id, decimal kg) => new() { VoucherDetailId = id, VoucherId = header.VoucherId,
            Voucher = header, ProductCode = "MAT", ProductName = "Material", LotNumber = "24102026", QtyKg = kg,
            VoucherType = VoucherDetailType.QCPass };
        var first = Line(34226, 6989); var second = Line(34302, 11); var pending = Line(34303, 20);
        var other = Line(34304, 30);
        other.Voucher = new() { VoucherId = 13329, CompanyId = Guid.NewGuid(), VoucherType = header.VoucherType };
        other.VoucherId = other.Voucher.VoucherId;
        store.Seed(nameof(db.WarehouseVouchers), header, other.Voucher);
        store.Seed(nameof(db.WarehouseVoucherDetails), first, second, pending, other);
        store.Seed(nameof(db.Materials), new Material { MaterialId = Guid.NewGuid(), CompanyId = company, ExternalId = "MAT", CategoryId = category });
        store.Seed(nameof(db.Categories), new Category { CategoryId = category, CompanyId = company });
        store.Seed(nameof(db.Employees), new Employee { EmployeeId = employee, CompanyId = company });
        store.Seed(nameof(db.QCInputByQCs), new[] { first, second, pending, other }.Select(d => new QCInputByQC {
            QCInputByQCId = Guid.NewGuid(), VoucherDetailId = d.VoucherDetailId, WarehouseVoucherDetail = d,
            CreatedDate = new(2026, 10, 26, 14, 48, 12), CreatedBy = employee }).ToArray());
        WarehouseShelfLedger Ledger(long id, Guid owner, DateTime date) => new() { VoucherDetailId = id, CompanyId = owner, CreatedAt = date };
        store.Seed(nameof(db.WarehouseShelfLedgers),
            Ledger(34226, company, new(2026, 10, 24, 14, 37, 22)),
            Ledger(34226, company, new(2026, 10, 25, 9, 0, 0)),
            Ledger(34302, company, new(2026, 10, 26, 13, 55, 34)),
            Ledger(34302, Guid.NewGuid(), new(2026, 11, 1)),
            Ledger(34304, other.Voucher.CompanyId, new(2026, 10, 26)));
        if (withTests)
            store.Seed(nameof(db.LabTests),
                new LabTest { Id = Guid.NewGuid(), CompanyId = company, Code = "NVL_TEST_00001", SampleName = "Sample", TestDate = new(2026, 10, 26), CreatedDate = new(2026, 10, 27), CreatedBy = employee },
                new LabTest { Id = Guid.NewGuid(), CompanyId = company, Code = "NVL_TEST_00002", SampleName = "Sample 2", TestDate = new(2026, 10, 27), CreatedDate = new(2026, 10, 28), CreatedBy = employee, BatchNumber = "LOT-TEST", WeightKg = 2.5m },
                new LabTest { Id = Guid.NewGuid(), CompanyId = Guid.NewGuid(), Code = "FOREIGN", TestDate = new(2026, 10, 27), CreatedDate = new(2026, 11, 1) });
        return new(db, company);
    }
}
