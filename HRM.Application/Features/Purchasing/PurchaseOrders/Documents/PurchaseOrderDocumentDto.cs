using HRM.Domain.Enums.Orders;
using HRM.Application.Features.Attachments.Dtos;
using HRM.Domain.Enums.Attachment;

namespace HRM.Application.Features.Purchasing.PurchaseOrders.Documents
{
    public class PurchaseOrderDocumentDto
    {
        public Guid PurchaseOrderDocumentId { get; set; }
        public Guid PurchaseOrderId { get; set; }
        public PurchaseOrderDocumentType DocumentType { get; set; }
        public string DocumentTypeName { get; set; } = string.Empty;
        public Guid? AttachmentCollectionId { get; set; }
        public string? DocumentCode { get; set; }
        public string? DocumentName { get; set; }
        public string? Note { get; set; }
        public bool IsReceived { get; set; }
        public DateTime? ReceivedDate { get; set; }
        public Guid? VerifiedBy { get; set; }
        public DateTime? VerifiedDate { get; set; }
        public DateTime CreatedDate { get; set; }
        public Guid CreatedBy { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public Guid? UpdatedBy { get; set; }
        public bool IsActive { get; set; }
        public IReadOnlyList<AttachmentDto> Attachments { get; set; } = new List<AttachmentDto>();
    }
}
