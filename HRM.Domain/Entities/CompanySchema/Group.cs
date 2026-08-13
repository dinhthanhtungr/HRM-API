using System;
using System.Collections.Generic;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.HrSchema;

namespace HRM.Domain.Entities.CompanySchema;

public partial class Group
{
    public Guid GroupId { get; set; }
    public Guid? PartId { get; set; }

    public string? GroupType { get; set; }

    public string ExternalId { get; set; } = null!;

    public string? Name { get; set; }

    public DateTime? CreatedDate { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public Guid? UpdatedBy { get; set; }

    public Guid? CompanyId { get; set; }

    //public bool IsActive { get; set; } = true;

    public virtual Company? Company { get; set; }

    public virtual Employee? CreatedByNavigation { get; set; }
    public virtual Part? Part { get; set; }

    public virtual ICollection<CustomerNote> CustomerNotes { get; set; } = new List<CustomerNote>();
    public virtual ICollection<CustomerClaim> CustomerClaims { get; set; } = new List<CustomerClaim>();
    public virtual ICollection<CustomerTransferLog> CustomerTransferLogFromGroups { get; set; } = new List<CustomerTransferLog>();

    public virtual ICollection<CustomerTransferLog> CustomerTransferLogToGroups { get; set; } = new List<CustomerTransferLog>();

    public virtual ICollection<MemberInGroup> MemberInGroups { get; set; } = new List<MemberInGroup>();
    public virtual ICollection<CustomerAssignment> CustomerAssignments { get; set; } = new List<CustomerAssignment>();

    public virtual Employee? UpdatedByNavigation { get; set; }
}
