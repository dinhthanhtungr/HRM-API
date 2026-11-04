using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.DevAndQA.IQC.Services;

internal sealed partial class IqcReader
{
    /// <summary>Ngày nhập theo lịch sử kho: ledger mới nhất của dòng phiếu trong company, không fallback ngày tạo header.</summary>
    private IQueryable<ReceiptDateRow> ReceiptDates => context.WarehouseShelfLedgers
        .AsNoTracking()
        .Where(x => x.CompanyId == _companyId && x.VoucherDetailId.HasValue)
        .GroupBy(x => x.VoucherDetailId!.Value)
        .Select(group => new ReceiptDateRow
        {
            VoucherDetailId = group.Key,
            InputDate = group.Max(x => (DateTime?)x.CreatedAt)
        });

    private sealed class ReceiptDateRow
    {
        public long VoucherDetailId { get; init; }
        public DateTime? InputDate { get; init; }
    }
}
