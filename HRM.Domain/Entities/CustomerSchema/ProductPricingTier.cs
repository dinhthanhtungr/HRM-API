namespace HRM.Domain.Entities.CustomerSchema;

public sealed class ProductPricingTier
{
    public Guid ProductPricingTierId { get; set; }
    public Guid ProductPricingVersionId { get; set; }

    public string QuantityRangeLabel { get; set; } = string.Empty;
    public decimal? MinQuantity { get; set; }
    public decimal? MaxQuantity { get; set; }
    public bool MinInclusive { get; set; } = true;
    public bool MaxInclusive { get; set; } = true;
    public decimal UnitPrice { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public ProductPricingVersion ProductPricingVersion { get; set; } = null!;
}
