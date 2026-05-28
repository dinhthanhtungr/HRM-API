using System;
using System.Collections.Generic;

namespace HRM.Domain.Entities.MaterialSchema;

public partial class SupplierContact
{
    public Guid ContactId { get; set; }

    public Guid SupplierId { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? Gender { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }
    public bool? IsPrimary { get; set; }
    public bool? IsActive { get; set; } = true;

    public virtual Supplier Supplier { get; set; } = null!;
}
