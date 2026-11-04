using HRM.Domain.Entities.AuditSchema;

namespace HRM.Domain.Entities.HrSchema;

public partial class Employee
{
    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    public virtual ICollection<EventLog> EventLogs { get; set; } = new List<EventLog>();
}
