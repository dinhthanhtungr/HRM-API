using HRM.Application.Abstractions.Persistence.Warehouse;
using HRM.Application.Features.Warehouse.Helpers.Publics;
using HRM.Domain.Entities.WarehouseSchema;
using HRM.Domain.Enums.WareHouses;

namespace HRM.Application.Features.Warehouse.Services;

internal static class ImsWarehouseVoucherScope
{
    public static IQueryable<WarehouseVoucher> Apply(
        IWarehouseReadDbContext db, IQueryable<WarehouseVoucher> vouchers, Guid companyId)
    {
        var purchaseCodes = db.PurchaseOrders.Where(x => x.CompanyId == companyId && x.ExternalId != null)
            .GroupBy(x => x.ExternalId).Where(x => x.Count() == 1).Select(x => x.Key);
        var deliveryCodes = db.DeliveryOrders.Where(x => x.CompanyId == companyId && x.ExternalId != null)
            .GroupBy(x => x.ExternalId).Where(x => x.Count() == 1).Select(x => x.Key);
        var requests = db.WarehouseRequests.Where(x => x.CompanyId == companyId && x.codeFromRequest != "");

        return vouchers.Where(v => v.CompanyId == companyId &&
            !db.OperationMaterialBuffers.Any(x => x.CompanyId == companyId && x.VoucherId == v.VoucherId) &&
            !db.ProductionOutputReceiptLedgers.Any(x => x.CompanyId == companyId && x.VoucherId == v.VoucherId) &&
            !db.WarehouseShelfLedgers.Any(x => x.CompanyId == companyId &&
                x.Shelf.CompanyId == companyId && x.Shelf.SlotCode == WarehouseStockQueryHelper.MixingShelfCode &&
                (x.VoucherId == v.VoucherId || db.WarehouseVoucherDetails.Any(d => d.VoucherId == v.VoucherId &&
                    d.VoucherDetailId == x.VoucherDetailId))) &&
            requests.Any(r => r.RequestId == v.RequestId &&
                (int)r.ReqType == v.VoucherType &&
                (((r.ReqType == WareHouseRequestType.ImportRawMaterial || r.ReqType == WareHouseRequestType.ImportOther ||
                   r.ReqType == WareHouseRequestType.ImportMaterial || r.ReqType == WareHouseRequestType.ExportReturn) &&
                  purchaseCodes.Contains(r.codeFromRequest)) ||
                 ((r.ReqType == WareHouseRequestType.ExportFinishedGood || r.ReqType == WareHouseRequestType.ExportDefectiveFinishedGood) &&
                  deliveryCodes.Contains(r.codeFromRequest)))));
    }
}
