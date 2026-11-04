using HRM.Application.Features.Purchasing.PurchaseOrders.Dtos;

namespace HRM.Application.Abstractions.Documents;

public interface IPurchaseOrderExcelRenderer
{
    byte[] RenderList(IReadOnlyList<PurchaseOrderExcelRow> rows);
    byte[] Render(PurchaseOrderDetailDto purchaseOrder);
}
