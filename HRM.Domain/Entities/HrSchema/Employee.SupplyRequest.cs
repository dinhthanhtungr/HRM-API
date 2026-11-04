using HRM.Domain.Entities.SupplyRequestSchema;

namespace HRM.Domain.Entities.HrSchema;

public partial class Employee
{
    public virtual ICollection<SupplyRequest> SupplyRequestCreatedByNavigations { get; set; } = new List<SupplyRequest>();
}
