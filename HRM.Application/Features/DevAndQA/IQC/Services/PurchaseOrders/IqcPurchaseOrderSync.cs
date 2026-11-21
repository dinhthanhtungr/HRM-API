using HRM.Application.Abstractions.Persistence.DevAndQA;
using HRM.Domain.Entities.WarehouseSchema;
using HRM.Domain.Enums.Devandqa;
using HRM.Domain.Enums.WareHouses;
using Microsoft.EntityFrameworkCore;
using HRM.Domain.Entities.OrderSchema;
using Shared.Enums;

namespace HRM.Application.Features.DevAndQA.IQC.Services;

/// <summary>Giữ luồng legacy: voucher → request.codeFromRequest → PO → material/LineNo.</summary>
internal sealed class IqcPurchaseOrderSync(IIqcDbContext context)
{
    public async Task SyncAsync(WarehouseVoucherDetail detail, QcDecision? decision,
        Guid companyId, Guid employeeId, CancellationToken ct)
    {
        var po = await FindRelatedAsync(detail, companyId, ct);
        if (po is null) return;
        var poCode = po.ExternalId!.Trim();
        var materialCode = detail.ProductCode?.Trim();

        var poDetail = po.PurchaseOrderDetails.Where(x => x.IsActive)
            .Where(x => (x.MaterialExternalIDSnapshot ?? "").Trim() == materialCode)
            .OrderBy(x => x.LineNo == detail.LineNo ? 0 : 1).FirstOrDefault();

        if (poDetail is null) return;

        var lineMaterialCode = poDetail.MaterialExternalIDSnapshot?.Trim();
        if (!string.IsNullOrWhiteSpace(lineMaterialCode))
        {
            var rows = await (
                from vd in context.WarehouseVoucherDetails.AsNoTracking()
                join v in context.WarehouseVouchers.AsNoTracking() on vd.VoucherId equals v.VoucherId
                join wr in context.WarehouseRequests.AsNoTracking() on v.RequestId equals wr.RequestId
                join qc in context.QCInputByQCs.AsNoTracking() on vd.VoucherDetailId equals qc.VoucherDetailId into qcGroup
                from qc in qcGroup.DefaultIfEmpty()
                where v.CompanyId == companyId && wr.CompanyId == companyId && wr.IsActive
                    && wr.ReqType != WareHouseRequestType.ExportReturn && v.VoucherType != (int)WareHouseRequestType.ExportReturn
                    && wr.codeFromRequest != null && wr.codeFromRequest.Trim() == poCode
                    && vd.ProductCode != null && vd.ProductCode.Trim() == lineMaterialCode
                select new { vd.VoucherDetailId, vd.QtyKg, Decision = qc != null ? qc.ImportWarehouseType : null }
            ).ToListAsync(ct);
            poDetail.RealQuantity = rows.Sum(x => IqcRules.IsAccepted(
                x.Decision ?? (x.VoucherDetailId == detail.VoucherDetailId ? decision : null)) ? x.QtyKg : 0m);
        }
        // Yêu cầu đổi hàng là quyết định chủ động, không bị phép tính số lượng ghi đè.
        var status = IqcRules.PurchaseOrderStatus(po.PurchaseOrderDetails);
        if (status is null || string.Equals(po.Status, PurchaseOrderStatus.ExchangeRequested, StringComparison.OrdinalIgnoreCase)) return;
        po.Status = status;
        po.UpdatedBy = employeeId;
        po.UpdatedDate = DateTime.Now;
    }

    /// <summary>Tìm DDH cùng company theo liên kết phiếu kho → yêu cầu kho → mã DDH.</summary>
    internal async Task<PurchaseOrder?> FindRelatedAsync(WarehouseVoucherDetail detail, Guid companyId, CancellationToken ct)
    {
        var requestId = await context.WarehouseVouchers.AsNoTracking()
            .Where(x => x.VoucherId == detail.VoucherId
                     && x.VoucherType != (int)WareHouseRequestType.ExportReturn
                     && x.CompanyId == companyId)
            .Select(x => x.RequestId).FirstOrDefaultAsync(ct);

        if (!requestId.HasValue) return null;

        var poCode = await context.WarehouseRequests.AsNoTracking()
            .Where(x => x.RequestId == requestId
                     && x.ReqType != WareHouseRequestType.ExportReturn
                     && x.CompanyId == companyId && x.IsActive)
            .Select(x => x.codeFromRequest).FirstOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(poCode)) return null;

        poCode = poCode.Trim();

        return await context.PurchaseOrders.Include(x => x.PurchaseOrderDetails)
            .FirstOrDefaultAsync(x => x.CompanyId == companyId
                                   && (x.IsActive ?? true)
                                   && x.ExternalId != null
                                   && x.ExternalId.Trim() == poCode, ct);
    }
}
