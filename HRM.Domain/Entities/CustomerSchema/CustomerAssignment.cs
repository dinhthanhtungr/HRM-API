using System;
using System.Collections.Generic;
using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.HrSchema;

namespace HRM.Domain.Entities.CustomerSchema;

public partial class CustomerAssignment
{
    public Guid Id { get; set; }

    public Guid CustomerId { get; set; }

    public Guid EmployeeId { get; set; }

    public Guid GroupId { get; set; }

    public DateTime CreatedDate { get; set; }

    public Guid CreatedBy { get; set; }

    public DateTime UpdatedDate { get; set; }

    public Guid UpdatedBy { get; set; }

    public Guid CompanyId { get; set; }

    public bool IsActive { get; set; } = true;

    public virtual Company Company { get; set; } = null!;

    public virtual Employee CreatedByNavigation { get; set; } = null!;

    public virtual Customer Customer { get; set; } = null!;

    public virtual Employee Employee { get; set; } = null!;

    public virtual Employee UpdatedByNavigation { get; set; } = null!;

    public virtual Group Group { get; set; } = null!;
}
