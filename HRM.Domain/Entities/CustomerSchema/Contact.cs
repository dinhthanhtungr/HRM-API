using System;
using System.Collections.Generic;

namespace HRM.Domain.Entities.CustomerSchema;

public partial class Contact
{
    public Guid ContactId { get; set; }

    public Guid CustomerId { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? Gender { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public bool? IsPrimary { get; set; }
    public bool IsActive { get; set; }

    public virtual Customer Customer { get; set; } = null!;
}
