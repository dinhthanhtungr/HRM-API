namespace HRM.Application.Features.CRM.Quotations.Dtos;

public sealed class ProductPricingTierRequest
{
    public string QuantityRangeLabel { get; init; } = string.Empty;
    public decimal? MinQuantity { get; init; }
    public decimal? MaxQuantity { get; init; }
    public bool MinInclusive { get; init; } = true;
    public bool MaxInclusive { get; init; } = true;
    public decimal UnitPrice { get; init; }
    public int SortOrder { get; init; }
}
