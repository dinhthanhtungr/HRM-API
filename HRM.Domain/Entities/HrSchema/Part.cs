using System;
using System.Collections.Generic;
using HRM.Domain.Entities.AuditSchema;
using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.MROSchema;
using HRM.Domain.Entities.Notifications;

namespace HRM.Domain.Entities.HrSchema;

public partial class Part
{
    public Guid PartId { get; set; }

    public string ExternalId { get; set; } = string.Empty;

    public string PartName { get; set; } = string.Empty;

    public string? Description { get; set; }


    public virtual ICollection<Group> Groups { get; set; } = new List<Group>();
    public virtual ICollection<Employee> Employees { get; set; } = new List<Employee>();
    //public virtual ICollection<EquipmentMRO> Equipments { get; set; } = new List<EquipmentMRO>();
    public virtual ICollection<EventLog> EventLogs { get; set; } = new List<EventLog>();
    public virtual ICollection<NotificationRecipient> NotificationRecipients { get; set; } = new List<NotificationRecipient>();
}
