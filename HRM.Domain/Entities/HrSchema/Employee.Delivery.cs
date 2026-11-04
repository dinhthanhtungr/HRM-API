using HRM.Domain.Entities.DeliverySchema;

namespace HRM.Domain.Entities.HrSchema;

public partial class Employee
{
    public virtual ICollection<DeliveryOrder> DeliveryOrderCreatedByNavigations { get; set; } = new List<DeliveryOrder>();
    public virtual ICollection<DeliveryOrder> DeliveryOrderUpdatedByNavigations { get; set; } = new List<DeliveryOrder>();
}
