using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.Formulas;

namespace HRM.Domain.Entities.CustomerSchema;

public sealed class FormulaPricingPolicy
{
    public Guid FormulaPricingPolicyId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid? CategoryId { get; set; }
    public FormulaPricingProfile Profile { get; set; }
    public string Currency { get; set; } = "VND";
    public string Name { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public decimal DefaultManufacturingCost { get; set; }
    public decimal DefaultProfitMarginRate { get; set; }
    public FormulaPricingRoundingRule RoundingRule { get; set; }
    public decimal RoundingIncrement { get; set; } = 1m;
    public FormulaPricingPolicyStatus Status { get; set; } = FormulaPricingPolicyStatus.Draft;
    public DateTime? EffectiveFrom { get; set; }
    public int? PriceValidityDays { get; set; }
    public Guid? PublishedBy { get; set; }
    public DateTime? PublishedAt { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime? UpdatedDate { get; set; }

    public Company Company { get; set; } = null!;
    public Category? Category { get; set; }
    public Employee CreatedByNavigation { get; set; } = null!;
    public Employee? UpdatedByNavigation { get; set; }
    public Employee? PublishedByNavigation { get; set; }
    public ICollection<FormulaPricingPolicyTier> Tiers { get; set; } = [];
    public ICollection<ProductPricingVersion> ProductPricingVersions { get; set; } = [];
}
