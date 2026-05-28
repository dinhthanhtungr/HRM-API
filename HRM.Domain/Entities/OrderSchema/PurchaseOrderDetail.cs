using System;
using System.Collections.Generic;
using HRM.Domain.Entities.MaterialSchema;

namespace HRM.Domain.Entities.OrderSchema;

public partial class PurchaseOrderDetail
{
    public Guid PurchaseOrderDetailId { get; set; }

    public Guid PurchaseOrderId { get; set; }

    public string? Package { get; set; }
    public int LineNo { get; set; }

    public decimal? RequestQuantity { get; set; }
    public decimal? RealQuantity { get; set; }

    public decimal? BaseCostSnapshot { get; set; }
    public DateTime? BaseDateSnapshot { get; set; }

    public decimal? UnitPriceAgreed { get; set; }
    public decimal? TotalPriceAgreed { get; set; }

    public Guid MaterialId { get; set; }
    public string? MaterialExternalIDSnapshot { get; set; }
    public string? MaterialNameSnapshot { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime? DeliveryDate { get; set; }

    public string? Note { get; set; }

    public virtual Material Material { get; set; } = null!;

    public virtual PurchaseOrder PurchaseOrder { get; set; } = null!;
}
