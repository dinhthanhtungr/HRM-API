using HRM.Application.Features.CRM.CustomerCare.Services.Search;
using HRM.Application.Commons.Searching;
using HRM.Domain.Entities.OrderSchema;

namespace HRM.Application.Features.PLM.SaleOrders.Services;

internal static class SaleOrderSmartSearch
{
    internal static SmartSearchSchema<MerchandiseOrder> Build(IQueryable<MerchandiseOrder> visible, Guid companyId)
        => SmartSearch.For(visible, fields => [
            fields.Code("saleOrderCode", x => x.ExternalId, "Mã đơn hàng", "Order code"),
            fields.Code("purchaseOrderNumber", x => x.PONo, "Số PO", "PO"),
            fields.Code("customerCode", x => x.CustomerExternalIdSnapshot, "Mã KH", "Customer code"),
            fields.Name("customerName", x => x.CustomerNameSnapshot, "Tên KH", "Customer name"),
            fields.CustomerTaxNumber(x => x.Customer != null && x.Customer.CompanyId == companyId ? x.Customer.TaxNumber : null),
            fields.Codes("colourCode", x => x.MerchandiseOrderDetails.Where(d => d.IsActive && d.Product != null && d.Product.CompanyId == companyId),
                d => d.Product.ColourCode, "ColorCode", "Mã màu"),
            fields.Names("productName", x => x.MerchandiseOrderDetails.Where(d => d.IsActive && d.Product != null && d.Product.CompanyId == companyId),
                d => d.Product.Name, "Tên sản phẩm", "Product name")
        ]);
}
