using HRM.Domain.Entities.Notifications;

namespace HRM.Domain.Entities.HrSchema;

public partial class Employee
{
    public virtual ICollection<Notification> CreatedByEmployeeNavigations { get; set; } = new List<Notification>();
}
