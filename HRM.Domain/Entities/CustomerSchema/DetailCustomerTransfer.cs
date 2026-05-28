using System;
using System.Collections.Generic;

namespace HRM.Domain.Entities.CustomerSchema;

public partial class DetailCustomerTransfer
{
    public Guid CustomerId { get; set; }

    public Guid LogId { get; set; }

    public virtual Customer Customer { get; set; } = null!;

    public virtual CustomerTransferLog Log { get; set; } = null!;
}
