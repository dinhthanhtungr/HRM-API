using HRM.Application.Features.DevAndQA.IQC.Dtos;
using HRM.Application.Commons.Searching;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.DevAndQA.IQC.Services;

internal sealed partial class IqcReader
{
    /// <summary>Ghép khóa hai nguồn trước count/page; chỉ tải metadata của trang hiện tại, không tải kết quả đo.</summary>
    internal IQueryable<SummarySourceKey> BuildCombinedSummaryKeys(QCInputQuery query)
    {
        var warehouseIds = BuildSummaryIdsQuery(query);
        // Join grouped receipt dates for sorting instead of a correlated MAX for every candidate voucher line.
        // A value-type projection keeps missing receipt dates nullable in both SQL and in-memory tests.
        var receiptDates = ReceiptDates.Select(x => new CombinedReceiptDate
        {
            VoucherDetailId = x.VoucherDetailId,
            InputDate = x.InputDate
        });
        var warehouse =
            from detail in WarehouseVoucherDetails.AsNoTracking()
            where warehouseIds.Contains(detail.VoucherDetailId)
            join receipt0 in receiptDates on detail.VoucherDetailId equals receipt0.VoucherDetailId into receipts
            from receipt in receipts.DefaultIfEmpty()
            select new SummarySourceKey
            {
                VoucherDetailId = detail.VoucherDetailId,
                LabTestId = null,
                SortDate = receipt.InputDate ?? detail.Voucher.CreatedDate
            };

        var labs = context.LabTests.AsNoTracking().Where(x => x.CompanyId == _companyId);
        // Những bộ lọc đặc thù kho/QC không áp dụng giả cho mẫu test độc lập.
        if (query.VoucherDetailId > 0 || query.QCInputByQCId.HasValue || query.VoucherDetailType.HasValue || query.HasQC == false)
            labs = labs.Where(x => false);
        labs = labs.WhereKeyword(query.Keyword, term => x =>
            EF.Functions.ILike(x.Code, term.ContainsPattern, PostgresSearchPattern.EscapeCharacter) ||
            EF.Functions.ILike(x.MaterialCode ?? "", term.ContainsPattern, PostgresSearchPattern.EscapeCharacter) ||
            EF.Functions.ILike(x.SampleName, term.ContainsPattern, PostgresSearchPattern.EscapeCharacter) ||
            EF.Functions.ILike(x.BatchNumber ?? "", term.ContainsPattern, PostgresSearchPattern.EscapeCharacter));
        if (query.FromDate.HasValue)
        {
            var from = DateOnly.FromDateTime(query.FromDate.Value);
            labs = labs.Where(x => x.TestDate >= from);
        }
        if (query.ToDate.HasValue)
        {
            var to = DateOnly.FromDateTime(query.ToDate.Value);
            labs = labs.Where(x => x.TestDate <= to);
        }
        var keys = warehouse.Concat(labs.Select(x => new SummarySourceKey
        {
            VoucherDetailId = 0,
            LabTestId = x.Id,
            SortDate = x.CreatedDate
        }));
        return keys;
    }

    private async Task<IqcPagedResult<GetSummaryQCInput>> GetCombinedSummaryAsync(QCInputQuery query, CancellationToken ct)
    {
        var pageNumber = query.PageNumber <= 0 ? 1 : query.PageNumber;
        var pageSize = query.PageSize <= 0 ? 20 : query.PageSize;
        var keys = BuildCombinedSummaryKeys(query);
        var total = await keys.CountAsync(ct);
        var page = await keys.OrderByDescending(x => x.SortDate)
            .ThenByDescending(x => x.VoucherDetailId).ThenByDescending(x => x.LabTestId)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return await LoadSummaryPageAsync(page, total, pageNumber, pageSize, ct);
    }

    private async Task<IqcPagedResult<GetSummaryQCInput>> LoadSummaryPageAsync(
        IReadOnlyList<SummarySourceKey> page, int total, int pageNumber, int pageSize, CancellationToken ct)
    {
        var voucherIds = page.Where(x => x.LabTestId is null).Select(x => x.VoucherDetailId).ToArray();
        var testIds = page.Where(x => x.LabTestId.HasValue).Select(x => x.LabTestId!.Value).ToArray();
        var warehouseRows = await BuildSummaryItemsQuery(voucherIds).ToListAsync(ct);
        foreach (var row in warehouseRows)
            row.QCDescription = IqcRules.ToVietnamese(row.VoucherType);

        var testRows = await (
            from test in context.LabTests.AsNoTracking()
            where test.CompanyId == _companyId && testIds.Contains(test.Id)
            join employee0 in Employees.AsNoTracking() on test.CreatedBy equals employee0.EmployeeId into employees
            from employee in employees.DefaultIfEmpty()
            select new GetSummaryQCInput
            {
                Source = "LabTest", LabTestId = test.Id, TestDate = test.TestDate,
                ExternalId = test.Code, Name = test.SampleName, LotNumber = test.BatchNumber ?? "",
                WeightKg = test.WeightKg, Unit = test.WeightKg.HasValue ? "kg" : "",
                HasQC = true, QCCreatedDate = test.CreatedDate,
                QCEmployeeName = employee == null ? "" : employee.FullName ?? ""
            }).ToListAsync(ct);
        var byVoucher = warehouseRows.ToDictionary(x => x.VoucherDetailId);
        var byTest = testRows.ToDictionary(x => x.LabTestId!.Value);
        var items = page.Select(x => x.LabTestId is { } id ? byTest.GetValueOrDefault(id) : byVoucher.GetValueOrDefault(x.VoucherDetailId))
            .Where(x => x is not null).Select(x => x!).ToList();
        return new(items, total, pageNumber, pageSize);
    }

    internal sealed class SummarySourceKey
    {
        public long VoucherDetailId { get; init; }
        public Guid? LabTestId { get; init; }
        public DateTime SortDate { get; init; }
    }

    private readonly record struct CombinedReceiptDate
    {
        public long VoucherDetailId { get; init; }
        public DateTime? InputDate { get; init; }
    }
}
