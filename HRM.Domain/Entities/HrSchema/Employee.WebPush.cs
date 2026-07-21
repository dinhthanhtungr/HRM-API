using HRM.Domain.Entities.Notifications;

namespace HRM.Domain.Entities.HrSchema;

public partial class Employee
{
    public virtual ICollection<WebPushSubscription> WebPushSubscriptions { get; set; } = new List<WebPushSubscription>();
}
