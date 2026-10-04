using HRM.Application.Features.Dispatch.DeliveryOrders.Dtos;

namespace HRM.Application.Abstractions.Documents;

public interface IDeliveryOrderPdfRenderer
{
    byte[] Render(DeliveryOrderDocumentDto document);
}
