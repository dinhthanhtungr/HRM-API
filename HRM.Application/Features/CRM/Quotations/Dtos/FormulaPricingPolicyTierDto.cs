namespace HRM.Application.Features.CRM.Quotations.Dtos;

public sealed class FormulaPricingPolicyTierDto
{
    public Guid FormulaPricingPolicyTierId { get; init; }
    public string QuantityRangeLabel { get; init; } = string.Empty;
    public decimal? MinQuantity { get; init; }
    public decimal? MaxQuantity { get; init; }
    public bool MinInclusive { get; init; }
    public bool MaxInclusive { get; init; }
    public decimal PriceOffset { get; init; }
    public bool RequiresManualPrice { get; init; }
    public bool IsActive { get; init; }
    public int SortOrder { get; init; }
}
