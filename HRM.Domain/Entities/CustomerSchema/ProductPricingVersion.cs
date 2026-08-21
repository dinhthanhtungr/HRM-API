using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Domain.Entities.CustomerSchema;

public sealed class ProductPricingVersion
{
    public Guid ProductPricingVersionId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? FormulaPricingPolicyId { get; set; }
    public bool HasManualTierAdjustment { get; set; }

    public Guid? SourceFormulaId { get; set; }
    public Guid? SourceManufacturingFormulaId { get; set; }
    public Guid? SourceSampleTrialId { get; set; }
    public Guid? SourceManufacturingVUFormulaId { get; set; }

    public string? FormulaExternalIdSnapshot { get; set; }
    public string? BatchNoSnapshot { get; set; }
    public string Currency { get; set; } = "VND";

    public decimal? MaterialCostSnapshot { get; set; }
    public decimal? ManufacturingCost { get; set; }
    public decimal? StandardSellingPrice { get; set; }
    public decimal? ProfitMarginRate { get; set; }

    public ProductPricingStatus Status { get; set; } = ProductPricingStatus.Draft;
    public int Version { get; set; } = 1;
    public DateTime? CalculatedAt { get; set; }

    public Guid? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public bool IsActive { get; set; } = true;
    public Guid CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime? UpdatedDate { get; set; }

    public Company Company { get; set; } = null!;
    public Product Product { get; set; } = null!;
    public FormulaPricingPolicy? FormulaPricingPolicy { get; set; }
    public Formula? SourceFormula { get; set; }
    public ManufacturingFormula? SourceManufacturingFormula { get; set; }
    public SampleRequestSampleTrial? SourceSampleTrial { get; set; }
    public ManufacturingVUFormula? SourceManufacturingVUFormula { get; set; }
    public Employee CreatedByNavigation { get; set; } = null!;
    public Employee? UpdatedByNavigation { get; set; }
    public Employee? ApprovedByNavigation { get; set; }

    public ICollection<ProductPricingTier> PriceTiers { get; set; } = [];
    public ICollection<QuotationLine> QuotationLines { get; set; } = [];
}
