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
