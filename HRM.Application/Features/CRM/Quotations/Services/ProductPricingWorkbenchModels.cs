using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Features.CRM.Quotations.Services;

internal sealed class ProductRow
{
    public Guid ProductId { get; init; }
    public string ProductCode { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public DateTime? CreatedDate { get; init; }
    public DateTime? UpdatedDate { get; init; }
    public DateTime? LatestSampleRequestCreatedDate { get; init; }
    public bool HasEligiblePricingSource { get; init; }
}

internal sealed class PricingVersionRow
{
    public Guid ProductPricingVersionId { get; init; }
    public Guid ProductId { get; init; }
    public ProductPricingSourceType? SourceType { get; init; }
    public Guid? SourceId { get; init; }
    public string? SourceExternalId { get; init; }
    public decimal? MaterialCostSnapshot { get; init; }
    public decimal? ManufacturingCost { get; init; }
    public decimal? StandardSellingPrice { get; init; }
    public decimal? ProfitMarginRate { get; init; }
    public ProductPricingStatus Status { get; init; }
    public int Version { get; init; }
    public DateTime CreatedDate { get; init; }
    public DateTime? UpdatedDate { get; init; }
}

internal sealed record CurrentPricingRows(
    PricingVersionRow? Draft,
    PricingVersionRow? Approved)
{
    public PricingVersionRow? Preferred => Draft ?? Approved;
}
