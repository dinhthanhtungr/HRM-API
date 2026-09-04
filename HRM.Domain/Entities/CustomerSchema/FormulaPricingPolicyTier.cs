namespace HRM.Domain.Entities.CustomerSchema;

public sealed class FormulaPricingPolicyTier
{
    public Guid FormulaPricingPolicyTierId { get; set; }
    public Guid FormulaPricingPolicyId { get; set; }
    public string QuantityRangeLabel { get; set; } = string.Empty;
    public decimal? MinQuantity { get; set; }
    public decimal? MaxQuantity { get; set; }
    public bool MinInclusive { get; set; } = true;
    public bool MaxInclusive { get; set; } = true;
    public decimal? PriceOffset { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public FormulaPricingPolicy FormulaPricingPolicy { get; set; } = null!;
}
