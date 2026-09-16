using HRM.Application.Features.Purchasing.PurchaseOrders.Dtos;

namespace HRM.Application.Abstractions.Documents;

public interface IPurchaseOrderPdfRenderer
{
    byte[] Render(PurchaseOrderDetailDto purchaseOrder);
}
