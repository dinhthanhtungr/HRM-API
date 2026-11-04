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

    private static IqcReader Fixture()
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
        return new(db, company);
    }
}
