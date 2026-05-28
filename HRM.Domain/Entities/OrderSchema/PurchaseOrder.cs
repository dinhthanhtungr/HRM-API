using System;
using System.Collections.Generic;
using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.MaterialSchema;

namespace HRM.Domain.Entities.OrderSchema;

public partial class PurchaseOrder
{
    public Guid PurchaseOrderId { get; set; }

    public string? ExternalId { get; set; }

    public string? OrderType { get; set; }

    public Guid? SupplierId { get; set; }

    public string? Comment { get; set; }
    public string? PLPUComment { get; set; }

    public DateTime? RequestDeliveryDate { get; set; }
    public DateTime? RealDeliveryDate { get; set; }

    public string? Status { get; set; }

    public Guid? CompanyId { get; set; }

    public DateTime? CreateDate { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public Guid? UpdatedBy { get; set; }
    public Guid? PurchaseOrderSnapshotId { get; set; }

    public bool? IsActive { get; set; } = true;
    public virtual PurchaseOrderSnapshot? PurchaseOrderSnapshot { get; set; }
    public virtual Company? Company { get; set; }

    public virtual Employee? CreatedByNavigation { get; set; }

    public virtual ICollection<PurchaseOrderDetail> PurchaseOrderDetails { get; set; } = new List<PurchaseOrderDetail>();
    public virtual ICollection<PurchaseOrderLink> PurchaseOrderLinks { get; set; } = new List<PurchaseOrderLink>();
    public virtual Supplier? Supplier { get; set; }

    public virtual Employee? UpdatedByNavigation { get; set; }
}
