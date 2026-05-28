using System;
using System.Collections.Generic;
using HRM.Domain.Entities.HrSchema;

namespace HRM.Domain.Entities.MaterialSchema;

public partial class PriceHistory
{
    public Guid PriceHistoryId { get; set; }

    public Guid MaterialsSuppliersId { get; set; }

    public decimal? OldPrice { get; set; }

    public string? Currency { get; set; }

    public DateTime? CreateDate { get; set; }

    public Guid? CreatedBy { get; set; }

    public virtual Employee? CreatedByNavigation { get; set; }

    public virtual MaterialsSupplier MaterialsSuppliers { get; set; } = null!;

}
