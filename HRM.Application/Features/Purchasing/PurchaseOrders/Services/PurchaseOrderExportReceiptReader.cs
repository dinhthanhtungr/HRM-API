using HRM.Application.Abstractions.Persistence.Purchasing;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.Purchasing.PurchaseOrders.Dtos;
using HRM.Domain.Enums.Devandqa;
using HRM.Domain.Enums.WareHouses;
using Microsoft.EntityFrameworkCore;
namespace HRM.Application.Features.Purchasing.PurchaseOrders.Services;

internal sealed class PurchaseOrderExportReceiptReader(IPurchaseOrderDbContext db)
{
    public async Task<List<PurchaseOrderReceiptExportRow>> BuildPurchaseOrderReceiptRowsAsync(
        List<string> poExternalIds, Guid companyId,
        CancellationToken ct)
    {
        var purchaseReceiptVoucherTypes = new[]
        {
            (int)WareHouseRequestType.ImportRawMaterial,
            (int)WareHouseRequestType.ImportOther,
            (int)WareHouseRequestType.ImportMaterial
        };

        var rawRows = await (
            from wr in db.WarehouseRequests.AsNoTracking()
            join v in db.WarehouseVouchers.AsNoTracking()
                on wr.RequestId equals v.RequestId
            join vd in db.WarehouseVoucherDetails.AsNoTracking()
                on v.VoucherId equals vd.VoucherId
            join ledger in db.WarehouseShelfLedgers.AsNoTracking()
                on (long?)vd.VoucherDetailId equals ledger.VoucherDetailId
            let qcLatest = db.QCInputByQCs.AsNoTracking()
                .Where(qc => qc.VoucherDetailId == vd.VoucherDetailId)
                .OrderByDescending(qc => qc.CreatedDate)
                .Select(qc => new
                {
                    qc.ImportWarehouseType,
                    qc.CreatedDate
                })
                .FirstOrDefault()
            where wr.CompanyId == companyId && v.CompanyId == companyId && ledger.CompanyId == companyId && wr.IsActive
                  && purchaseReceiptVoucherTypes.Contains(v.VoucherType)
                  && ledger.DeltaKg > 0m
                  && poExternalIds.Contains((wr.codeFromRequest ?? "").Trim())
            select new PurchaseOrderReceiptExportRow
            {
                POExternalId = (wr.codeFromRequest ?? "").Trim(),
                VoucherDetailId = ledger.VoucherDetailId ?? vd.VoucherDetailId,
                ProductCode = ledger.ProductCode ?? vd.ProductCode ?? string.Empty,
                ProductName = vd.ProductName ?? string.Empty,
                LotNumber = ledger.LotNumber ?? vd.LotNumber ?? string.Empty,
                ReceiptDate = ledger.CreatedAt,
                ReceivedQuantity = ledger.DeltaKg,
                QCResult = qcLatest != null ? qcLatest.ImportWarehouseType : null,
                VoucherDetailType = vd.VoucherType
            })
            .ToListAsync(ct);

        return rawRows
            .GroupBy(x => new
            {
                POExternalId = PurchaseOrderExportRules.NormalizePurchaseOrderKey(x.POExternalId),
                x.VoucherDetailId,
                ProductCode = PurchaseOrderExportRules.NormalizePurchaseOrderKey(x.ProductCode),
                ProductName = PurchaseOrderExportRules.NormalizePurchaseOrderKey(x.ProductName),
                LotNumber = PurchaseOrderExportRules.NormalizePurchaseOrderKey(x.LotNumber),
                x.QCResult,
                x.VoucherDetailType
            })
            .Select(g =>
            {
                var first = g.First();
                var row = new PurchaseOrderReceiptExportRow
                {
                    POExternalId = first.POExternalId,
                    VoucherDetailId = g.Key.VoucherDetailId,
                    ProductCode = first.ProductCode,
                    ProductName = first.ProductName,
                    LotNumber = first.LotNumber,
                    ReceiptDate = g.Min(x => x.ReceiptDate),
                    ReceivedQuantity = g.Sum(x => x.ReceivedQuantity),
                    QCResult = g.Key.QCResult,
                    VoucherDetailType = g.Key.VoucherDetailType
                };

                PurchaseOrderExportRules.ApplyPurchaseOrderReceiptQcQuantities(row);
                return row;
            })
            .ToList();
    }

}
