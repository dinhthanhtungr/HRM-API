using HRM.Domain.Enums.Orders;
using HRM.Domain.Enums.Attachment;

namespace HRM.Application.Features.Purchasing.PurchaseOrders.Documents
{
    public class CreatePurchaseOrderDocumentRequest
    {
        public PurchaseOrderDocumentType DocumentType { get; set; } = PurchaseOrderDocumentType.Other;
        public Guid? AttachmentCollectionId { get; set; }
        public string? DocumentCode { get; set; }
        public string? DocumentName { get; set; }
        public string? Note { get; set; }
        public bool IsReceived { get; set; }
        public DateTime? ReceivedDate { get; set; }
    }
}
