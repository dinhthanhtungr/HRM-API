using System;
using System.Collections.Generic;

namespace HRM.Domain.Entities.CustomerSchema;

public partial class Address
{
    public Guid AddressId { get; set; }

    public Guid CustomerId { get; set; }

    public string? AddressLine { get; set; }

    public string? City { get; set; }

    public string? District { get; set; }

    public string? Province { get; set; }

    public string? Country { get; set; }

    public bool? IsPrimary { get; set; }
    public bool IsActive { get; set; }

    public string? PostalCode { get; set; }

    public virtual Customer Customer { get; set; } = null!;
}
