using System;
using System.Collections.Generic;
using HRM.Domain.Entities.HrSchema;

namespace HRM.Domain.Entities.MaterialSchema;

public partial class MaterialsSupplier
{
    public Guid MaterialsSuppliersId { get; set; }

    public Guid SupplierId { get; set; }

    public Guid MaterialId { get; set; }

    public int? MinDeliveryDays { get; set; }

    public decimal? CurrentPrice { get; set; }

    public string? Currency { get; set; }

    public DateTime? CreateDate { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public Guid? UpdatedBy { get; set; }

    public bool? IsPreferred { get; set; }
    public bool? IsActive { get; set; }

    public virtual Material Material { get; set; } = null!;

    public virtual Supplier Supplier { get; set; } = null!;

    public virtual Employee? UpdatedByNavigation { get; set; }
    public virtual Employee? CreatedByNavigation { get; set; }

    public virtual ICollection<PriceHistory> PriceHistories { get; set; } = new List<PriceHistory>();
}
