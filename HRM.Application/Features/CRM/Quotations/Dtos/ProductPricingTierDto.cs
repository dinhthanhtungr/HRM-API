namespace HRM.Application.Features.CRM.Quotations.Dtos;

public sealed class ProductPricingTierDto
{
    public Guid ProductPricingTierId { get; init; }
    public string QuantityRangeLabel { get; init; } = string.Empty;
    public decimal? MinQuantity { get; init; }
    public decimal? MaxQuantity { get; init; }
    public bool MinInclusive { get; init; }
    public bool MaxInclusive { get; init; }
    public decimal UnitPrice { get; init; }
    public int SortOrder { get; init; }
}
