using System;
using System.Collections.Generic;
using HRM.Domain.Entities.AttachmentSchema;
using HRM.Domain.Entities.DeliverySchema;
using HRM.Domain.Entities.SampleRequestSchema;

namespace HRM.Domain.Entities.OrderSchema;

public partial class MerchandiseOrderDetail
{
    public Guid MerchandiseOrderDetailId { get; set; }

    public Guid MerchandiseOrderId { get; set; }
    public Guid? ComplaintReportLineId { get; set; }

    public Guid ProductId { get; set; }
    public string ProductExternalIdSnapshot { get; set; } = string.Empty;
    public string ProductNameSnapshot { get; set; } = string.Empty;

    public Guid FormulaId { get; set; }
    public string FormulaExternalIdSnapshot { get; set; } = string.Empty;

    public decimal ExpectedQuantity { get; set; }
    public decimal? RealQuantity { get; set; }

    public string BagType { get; set; } = string.Empty;
    public string PackageWeight { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string? Comment { get; set; }

    public DateTime DeliveryRequestDate { get; set; }
    public DateTime? DeliveryActualDate { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }

    public decimal BaseCostSnapshot { get; set; }
    public decimal RecommendedUnitPrice { get; set; }
    public decimal UnitPriceAgreed { get; set; }
    public decimal TotalPriceAgreed { get; set; }

    public bool IsActive { get; set; } = true;

    public virtual ICollection<DeliveryOrderDetail> DeliveryOrderDetails { get; set; } = new List<DeliveryOrderDetail>();
    public virtual ICollection<ComplaintReportLine> ComplaintReportLines { get; set; } = new List<ComplaintReportLine>();
    public virtual ComplaintReportLine? ComplaintReportLine { get; set; }
    public virtual MerchandiseOrder MerchandiseOrder { get; set; } = null!;
    public virtual Product Product { get; set; } = null!;
    public virtual Formula Formula { get; set; } = null!;
}
