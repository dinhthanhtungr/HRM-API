using System;
using System.Collections.Generic;
using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Domain.Entities.CustomerSchema;

public partial class CustomerTransferLog
{
    public Guid Id { get; set; }

    public Guid FromEmployeeId { get; set; }

    public Guid ToEmployeeId { get; set; }

    public Guid FromGroupId { get; set; }

    public Guid ToGroupId { get; set; }

    public string? Note { get; set; }
    public TransferType TransferType { get; set; }

    public DateTime CreatedDate { get; set; }

    public Guid CreatedBy { get; set; }

    public Guid CompanyId { get; set; }

    public virtual Company Company { get; set; } = null!;

    public virtual Employee CreatedByNavigation { get; set; } = null!;

    public virtual ICollection<DetailCustomerTransfer> DetailCustomerTransfers { get; set; } = new List<DetailCustomerTransfer>();

    public virtual Employee FromEmployee { get; set; } = null!;

    public virtual Group FromGroup { get; set; } = null!;

    public virtual Employee ToEmployee { get; set; } = null!;

    public virtual Group ToGroup { get; set; } = null!;
}
