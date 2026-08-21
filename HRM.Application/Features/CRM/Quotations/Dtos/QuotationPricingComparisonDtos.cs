using System.Text.Json.Serialization;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Features.CRM.Quotations.Dtos;

public sealed class QuotationPricingComparisonDto
{
    public Guid QuotationId { get; init; }
    public DateTime CalculatedAt { get; init; }
    public IReadOnlyList<QuotationLinePricingComparisonDto> Lines { get; init; } = [];
}

public sealed class QuotationLinePricingComparisonDto
{
    public Guid QuotationLineId { get; init; }
    public Guid ProductId { get; init; }
    public string ProductExternalId { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public QuotationLinePriceMode SavedPriceMode { get; init; }

    public decimal SavedUnitPrice { get; init; }
    public IReadOnlyList<QuotationLinePriceTierDto> SavedPriceTiers { get; init; } = [];

    public Guid? CurrentProductPricingVersionId { get; init; }
    public int? CurrentProductPricingVersion { get; init; }
    public bool IsUsingLatestApprovedPricing { get; init; }

    public Guid? FormulaId { get; init; }
    public string? FormulaExternalId { get; init; }
    public string? FormulaName { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public QuotationFormulaSelectionSource? FormulaSelectionSource { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public QuotationCurrentPricingStatus CurrentPricingStatus { get; init; }

    public bool IsCurrentPricingComplete { get; init; }
    public int MissingMaterialPriceCount { get; init; }
    public IReadOnlyList<QuotationCurrentPriceTierDto> CurrentPriceTiers { get; init; } = [];
    public bool? HasDifference { get; init; }
}

public sealed class QuotationCurrentPriceTierDto
{
    public string QuantityRangeLabel { get; init; } = string.Empty;
    public decimal? MinQuantity { get; init; }
    public decimal? MaxQuantity { get; init; }
    public bool MinInclusive { get; init; }
    public bool MaxInclusive { get; init; }
    public decimal? UnitPrice { get; init; }
    public bool RequiresManualPrice { get; init; }
    public int SortOrder { get; init; }
}
