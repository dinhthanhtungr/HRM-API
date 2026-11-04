using HRM.Domain.Enums.Orders;
using HRM.Domain.Entities.AttachmentSchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Enums.Attachment;

namespace HRM.Domain.Entities.OrderSchema;

public class PurchaseOrderDocument
{
    public Guid PurchaseOrderDocumentId { get; set; }

    public Guid PurchaseOrderId { get; set; }

    public PurchaseOrderDocumentType DocumentType { get; set; } = PurchaseOrderDocumentType.Other;

    public Guid? AttachmentCollectionId { get; set; }

    public string? DocumentCode { get; set; }

    public string? DocumentName { get; set; }

    public string? Note { get; set; }

    public bool IsReceived { get; set; }

    public DateTime? ReceivedDate { get; set; }

    public Guid? VerifiedBy { get; set; }

    public DateTime? VerifiedDate { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public Guid CreatedBy { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public Guid? UpdatedBy { get; set; }

    public bool IsActive { get; set; } = true;

    public virtual PurchaseOrder PurchaseOrder { get; set; } = null!;

    public virtual AttachmentCollection? AttachmentCollection { get; set; }

    public virtual Employee? VerifiedByNavigation { get; set; }

    public virtual Employee CreatedByNavigation { get; set; } = null!;

    public virtual Employee? UpdatedByNavigation { get; set; }
}
