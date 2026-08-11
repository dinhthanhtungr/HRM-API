using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Entities.ManufacturingSchema;

namespace HRM.Domain.Entities.OrderSchema;

public partial class ComplaintReportLine
{
    public Guid ComplaintReportLineId { get; set; }
    public Guid ComplaintReportId { get; set; }
    public Guid SourceMerchandiseOrderDetailId { get; set; }
    public Guid? SourceMfgProductionOrderId { get; set; }
    public Guid ProductId { get; set; }
    public Guid FormulaId { get; set; }
    public Guid? ManufacturingFormulaId { get; set; }
    public string ProductExternalIdSnapshot { get; set; } = string.Empty;
    public string ProductNameSnapshot { get; set; } = string.Empty;
    public string FormulaExternalIdSnapshot { get; set; } = string.Empty;
    public string? ManufacturingFormulaExternalIdSnapshot { get; set; }
    public decimal ComplaintQuantity { get; set; }
    public decimal? ApprovedReplacementQuantity { get; set; }
    public string? IssueType { get; set; }
    public string? Severity { get; set; }
    public string? Description { get; set; }
    public string? ResolutionNote { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime UpdatedDate { get; set; }
    public Guid UpdatedBy { get; set; }

    public virtual ComplaintReport ComplaintReport { get; set; } = null!;
    public virtual MerchandiseOrderDetail SourceMerchandiseOrderDetail { get; set; } = null!;
    public virtual MfgProductionOrder? SourceMfgProductionOrder { get; set; }
    public virtual Product Product { get; set; } = null!;
    public virtual Formula Formula { get; set; } = null!;
    public virtual ManufacturingFormula? ManufacturingFormula { get; set; }
    public virtual ICollection<ComplaintReportLineLot> Lots { get; set; } = new List<ComplaintReportLineLot>();
    public virtual ICollection<MerchandiseOrderDetail> ProcessingMerchandiseOrderDetails { get; set; } = new List<MerchandiseOrderDetail>();
}
