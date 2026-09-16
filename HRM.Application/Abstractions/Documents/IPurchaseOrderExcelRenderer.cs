using HRM.Application.Features.Purchasing.PurchaseOrders.Dtos;

namespace HRM.Application.Abstractions.Documents;

public interface IPurchaseOrderExcelRenderer
{
    byte[] Render(PurchaseOrderDetailDto purchaseOrder);
}
