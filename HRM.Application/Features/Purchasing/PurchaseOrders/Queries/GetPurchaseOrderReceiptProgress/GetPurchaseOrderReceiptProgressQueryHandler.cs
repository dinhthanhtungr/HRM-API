using HRM.Application.Abstractions.Persistence.Purchasing;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Purchasing.PurchaseOrders.Dtos;
using HRM.Application.Features.Purchasing.PurchaseOrders.Services;
using HRM.Domain.Enums.WareHouses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Purchasing.PurchaseOrders.Queries.GetPurchaseOrderReceiptProgress;

internal sealed class GetPurchaseOrderReceiptProgressQueryHandler(
    IPurchaseOrderDbContext db, ICurrentUser user, ICurrentUserPermissionService permissions)
    : IRequestHandler<GetPurchaseOrderReceiptProgressQuery, PurchaseOrderReceiptProgressDto?>
{
    private static readonly WareHouseRequestType[] ImportTypes =
        [WareHouseRequestType.ImportRawMaterial, WareHouseRequestType.ImportOther, WareHouseRequestType.ImportMaterial];

    public async Task<PurchaseOrderReceiptProgressDto?> Handle(GetPurchaseOrderReceiptProgressQuery request, CancellationToken ct)
    {
        if (!user.IsAuthenticated || !PurchaseOrderAccess.CanManage(permissions) || user.CompanyId is not { } company || company == Guid.Empty)
            throw new UnauthorizedAccessException("Không có quyền xem tiến độ nhập kho ĐĐH.");
        var order = await db.PurchaseOrders.AsNoTracking().Where(x => x.PurchaseOrderId == request.PurchaseOrderId &&
            x.CompanyId == company && (request.IncludeInactive || x.IsActive == true))
            .Select(x => new PurchaseOrderReceiptProgressDto { PurchaseOrderId = x.PurchaseOrderId,
                PurchaseOrderCode = x.ExternalId ?? "", OrderStatus = x.Status, IsActive = x.IsActive == true }).FirstOrDefaultAsync(ct);
        if (order is null) return null;
        var ordered = await db.PurchaseOrderDetails.AsNoTracking().Where(x => x.PurchaseOrderId == request.PurchaseOrderId && x.IsActive)
            .OrderBy(x => x.LineNo).ThenBy(x => x.PurchaseOrderDetailId)
            .Select(x => new ReceiptOrderedLine(x.PurchaseOrderDetailId,
                x.MaterialExternalIDSnapshot ?? (x.Material.CompanyId == company ? x.Material.ExternalId : null) ?? "",
                x.MaterialNameSnapshot ?? (x.Material.CompanyId == company ? x.Material.Name : null) ?? "",
                x.Material.CompanyId == company ? x.Material.Unit : null, x.RequestQuantity ?? 0)).ToListAsync(ct);
        var code = order.PurchaseOrderCode.Trim().ToUpperInvariant();
        if (code.Length == 0)
        {
            order.AssociationStatus = "MissingOrderCode";
            order.Warnings.Add("MissingOrderCode");
            return ReceiptProgressCalculator.Build(order, ordered, []);
        }
        // Include inactive duplicates: a string source cannot establish which historical PO owns the receipt.
        if (await db.PurchaseOrders.AsNoTracking().CountAsync(x => x.CompanyId == company && x.ExternalId != null &&
            x.ExternalId.Trim().ToUpper() == code, ct) != 1)
        {
            order.AssociationStatus = "AmbiguousOrderCode";
            order.Warnings.Add("DuplicateOrderCode");
            return ReceiptProgressCalculator.Build(order, ordered, []);
        }
        var requests = db.WarehouseRequests.AsNoTracking().Where(x => x.CompanyId == company && x.IsActive &&
            x.ReqStatus != WarehouseRequestStatus.Cancelled && ImportTypes.Contains(x.ReqType) &&
            x.codeFromRequest != null && x.codeFromRequest.Trim().ToUpper() == code);
        order.Requests = await requests.OrderBy(x => x.CreatedDate).ThenBy(x => x.RequestId)
            .Select(x => new ReceiptRequestDto(x.RequestId, x.RequestCode, (int)x.ReqType, (int)x.ReqStatus, x.CreatedDate)).ToListAsync(ct);
        if (order.Requests.Count == 0)
        {
            order.AssociationStatus = "NoRequests";
            return ReceiptProgressCalculator.Build(order, ordered, []);
        }
        var rows = await ReceiptRows(requests, company).ToListAsync(ct);
        var ids = rows.Select(x => x.VoucherDetailId).ToArray();
        if (ids.Length == 0) return ReceiptProgressCalculator.Build(order, ordered, rows);
        var inspections = await db.QCInputByQCs.AsNoTracking().Where(x => ids.Contains(x.VoucherDetailId))
            .Select(x => new ReceiptInspection(x.VoucherDetailId, x.QCInputByQCId, x.CreatedDate, x.ImportWarehouseType)).ToListAsync(ct);
        var movements = await db.WarehouseShelfLedgers.AsNoTracking().Where(x => x.CompanyId == company &&
            x.VoucherDetailId.HasValue && ids.Contains(x.VoucherDetailId.Value))
            .Select(x => new ReceiptMovement(x.VoucherDetailId!.Value, x.VoucherId, x.SlotId,
                x.Shelf != null && x.Shelf.CompanyId == company ? x.Shelf.SlotCode : null,
                x.ProductCode, x.LotNumber, x.StockType, x.DeltaKg, x.CreatedAt)).ToListAsync(ct);
        ReceiptProgressCalculator.ApplyEvidence(rows, inspections, movements);
        return ReceiptProgressCalculator.Build(order, ordered, rows);
    }
    internal IQueryable<ReceiptVoucherLineDto> ReceiptRows(IQueryable<HRM.Domain.Entities.WarehouseSchema.WarehouseRequest> requests, Guid company)
    {
        var importCodes = ImportTypes.Select(x => (int)x).ToArray();
        return (from r in requests
                          join v in db.WarehouseVouchers.AsNoTracking() on (int?)r.RequestId equals v.RequestId
                          join d in db.WarehouseVoucherDetails.AsNoTracking() on v.VoucherId equals d.VoucherId
                          where v.CompanyId == company && importCodes.Contains(v.VoucherType) && d.IsIncrease
                          orderby v.CreatedDate descending, v.VoucherId descending, d.LineNo, d.VoucherDetailId
                          select new ReceiptVoucherLineDto { RequestId = r.RequestId, RequestCode = r.RequestCode,
                              VoucherId = v.VoucherId, VoucherCode = v.VoucherCode, VoucherStatus = v.Status, VoucherDate = v.CreatedDate,
                              VoucherDetailId = d.VoucherDetailId, ProductCode = d.ProductCode, ProductName = d.ProductName,
                              LotNumber = d.LotNumber, Unit = d.UnitName, RecordedQuantity = d.QtyKg,
                              DetailType = (int)d.VoucherType, IsApplied = d.IsApplied,
                              SlotCode = d.Slot != null && d.Slot.CompanyId == company ? d.Slot.SlotCode : null });
    }

}
