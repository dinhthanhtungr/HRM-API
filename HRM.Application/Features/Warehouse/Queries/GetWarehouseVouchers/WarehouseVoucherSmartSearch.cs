using HRM.Application.Commons.Searching;
using HRM.Domain.Entities.WarehouseSchema;

namespace HRM.Application.Features.Warehouse.Queries.GetWarehouseVouchers;

internal static class WarehouseVoucherSmartSearch
{
    internal static SmartSearchSchema<WarehouseVoucherHeaderReadModel> Build(
        IQueryable<WarehouseVoucherHeaderReadModel> visible, IQueryable<WarehouseVoucherDetail> details)
        => SmartSearch.For(visible, fields => [
            fields.Code("voucherCode", x => x.VoucherCode, "Mã chứng từ", "Voucher code"),
            fields.Codes("itemCode", x => details.Where(d => d.VoucherId == x.VoucherId), d => d.ProductCode, "Mã hàng", "Item code"),
            fields.Names("itemName", x => details.Where(d => d.VoucherId == x.VoucherId), d => d.ProductName, "Tên hàng", "Item name"),
            fields.Codes("batchNumber", x => details.Where(d => d.VoucherId == x.VoucherId), d => d.LotNumber, "Số lô", "Batch")
        ]);
}
