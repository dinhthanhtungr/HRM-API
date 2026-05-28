using System;
using System.Collections.Generic;

namespace HRM.Domain.Entities.MaterialSchema;

public partial class SupplierAddress
{
    public Guid AddressId { get; set; }

    public Guid SupplierId { get; set; }

    public string? AddressLine { get; set; }

    public string? City { get; set; }

    public string? District { get; set; }

    public string? Province { get; set; }

    public string? Country { get; set; }

    public bool? IsPrimary { get; set; }

    public string? PostalCode { get; set; }

    public bool? IsActive { get; set; } = true;

    public virtual Supplier Supplier { get; set; } = null!;
}
