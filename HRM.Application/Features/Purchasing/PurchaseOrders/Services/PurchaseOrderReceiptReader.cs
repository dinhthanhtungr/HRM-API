using HRM.Domain.Enums.WareHouses;
using HRM.Application.Abstractions.Persistence.Purchasing;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Purchasing.PurchaseOrders.Services;

internal sealed record PurchaseOrderReceiptSummary(
    string PurchaseOrderCode,
    string ProductCode,
    decimal Quantity,
    DateTime LastReceiptDate);

internal sealed class PurchaseOrderReceiptReader(IPurchaseOrderDbContext dbContext)
{
    /// <summary>Ngày giao thực tế là ngày lập IQC mới nhất của các dòng nhập gắn PO, không lấy ngày ledger hoặc ngày nhập tay.</summary>
    public async Task<Dictionary<string, DateTime>> ReadIqcDatesAsync(
        Guid companyId, IReadOnlyCollection<string> purchaseOrderCodes, CancellationToken ct)
    {
        if (purchaseOrderCodes.Count == 0) return [];
        var rows = await (
            from request in dbContext.WarehouseRequests.AsNoTracking()
            join voucher in dbContext.WarehouseVouchers.AsNoTracking() on request.RequestId equals voucher.RequestId
            join detail in dbContext.WarehouseVoucherDetails.AsNoTracking() on voucher.VoucherId equals detail.VoucherId
            join qc in dbContext.QCInputByQCs.AsNoTracking() on detail.VoucherDetailId equals qc.VoucherDetailId
            where request.CompanyId == companyId && voucher.CompanyId == companyId && request.IsActive &&
                request.codeFromRequest != null && purchaseOrderCodes.Contains(request.codeFromRequest.Trim()) &&
                (voucher.VoucherType == (int)WareHouseRequestType.ImportRawMaterial ||
                 voucher.VoucherType == (int)WareHouseRequestType.ImportOther ||
                 voucher.VoucherType == (int)WareHouseRequestType.ImportMaterial)
            group qc by request.codeFromRequest.Trim() into inspections
            select new { Code = inspections.Key, Date = inspections.Max(x => x.CreatedDate) }
        ).ToListAsync(ct);
        return rows.ToDictionary(x => x.Code, x => x.Date);
    }

    public async Task<IReadOnlyList<PurchaseOrderReceiptSummary>> ReadAsync(
        Guid companyId,
        IReadOnlyCollection<string> purchaseOrderCodes,
        CancellationToken cancellationToken)
    {
        if (purchaseOrderCodes.Count == 0)
            return [];

        return await (
                from request in dbContext.WarehouseRequests.AsNoTracking()
                join voucher in dbContext.WarehouseVouchers.AsNoTracking()
                    on request.RequestId equals voucher.RequestId
                join detail in dbContext.WarehouseVoucherDetails.AsNoTracking()
                    on voucher.VoucherId equals detail.VoucherId
                join ledger in dbContext.WarehouseShelfLedgers.AsNoTracking()
                    on detail.VoucherDetailId equals ledger.VoucherDetailId
                where request.CompanyId == companyId &&
                      voucher.CompanyId == companyId &&
                      ledger.CompanyId == companyId &&
                      request.IsActive &&
                      purchaseOrderCodes.Contains(request.codeFromRequest) &&
                      (voucher.VoucherType == (int)WareHouseRequestType.ImportRawMaterial || voucher.VoucherType == (int)WareHouseRequestType.ImportOther || voucher.VoucherType == (int)WareHouseRequestType.ImportMaterial) &&
                      ledger.DeltaKg > 0
                group ledger by new { request.codeFromRequest, detail.ProductCode }
                into receipt
                select new PurchaseOrderReceiptSummary(
                    receipt.Key.codeFromRequest,
                    receipt.Key.ProductCode,
                    receipt.Sum(item => item.DeltaKg),
                    receipt.Max(item => item.CreatedAt)))
            .ToListAsync(cancellationToken);
    }
}
