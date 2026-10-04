using HRM.Application.Features.Dispatch.DeliveryOrders.Dtos;

namespace HRM.Application.Abstractions.Documents;

public interface IDeliveryOrderExcelRenderer
{
    byte[] Render(DeliveryOrderDocumentDto document);
}
