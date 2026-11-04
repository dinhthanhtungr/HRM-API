using HRM.Domain.Entities.OrderSchema;

namespace HRM.Domain.Entities.HrSchema;

public partial class Employee
{
    public virtual ICollection<MerchandiseOrder> MerchandiseOrderCreatedByNavigations { get; set; } = new List<MerchandiseOrder>();
    public virtual ICollection<MerchandiseOrder> MerchandiseOrderManagerBies { get; set; } = new List<MerchandiseOrder>();
    public virtual ICollection<MerchandiseOrder> MerchandiseOrderDeliveryPausedBies { get; set; } = new List<MerchandiseOrder>();
    public virtual ICollection<MerchandiseOrder> MerchandiseOrderUpdatedByNavigations { get; set; } = new List<MerchandiseOrder>();
    public virtual ICollection<PurchaseOrder> PurchaseOrderCreatedByNavigations { get; set; } = new List<PurchaseOrder>();
    public virtual ICollection<PurchaseOrder> PurchaseOrderUpdatedByNavigations { get; set; } = new List<PurchaseOrder>();
    public virtual ICollection<ComplaintReport> ComplaintReportCreatedByNavigations { get; set; } = new List<ComplaintReport>();
    public virtual ICollection<ComplaintReport> ComplaintReportUpdatedByNavigations { get; set; } = new List<ComplaintReport>();
    public virtual ICollection<ComplaintReport> ComplaintReportCompletedByNavigations { get; set; } = new List<ComplaintReport>();
}
