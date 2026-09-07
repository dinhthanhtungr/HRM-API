using System.Text.Json.Serialization;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Features.CRM.Quotations.Dtos;

/// <summary>
/// Preview một dòng báo giá chưa được lưu, có cùng contract hiển thị với <see cref="QuotationLineDto"/>
/// nhưng không chứa các ID chỉ được sinh sau khi persist quotation.
/// </summary>
public sealed class QuotationProductPricingLinePreviewDto
{
    public Guid ProductId { get; init; }
    public Guid? SampleRequestId { get; init; }
    public Guid? ProductPricingVersionId { get; init; }
    public int? ProductPricingVersion { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ProductPricingStatus? ProductPricingStatus { get; init; }

    public bool HasApprovedPricingAvailable { get; init; }
    public bool HasNewerPricingVersion { get; init; }
    public bool CanApplyToQuotation { get; init; }
    public bool CanUseManualCustomerPrice { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public QuotationPricingAvailability PricingAvailability { get; init; }

    public string? WarningCode { get; init; }
    public string? WarningMessage { get; init; }
    public string ProductExternalId { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public QuotationDefaultPriceTierSource? DefaultPriceTierSource { get; init; }

    public QuotationApprovedTierPricingDto? ApprovedPricing { get; init; }
    public QuotationSystemCalculatedTierPricingDto? SystemCalculatedPricing { get; init; }
    public QuotationLatestQuotedTierPricingDto? LatestQuotedPricing { get; init; }
    public IReadOnlyList<QuotationReferencePriceTierDto> ManualPriceTierTemplates { get; init; } = [];

    public decimal Quantity { get; init; }
    public string Unit { get; init; } = string.Empty;
    public QuotationLinePriceMode PriceMode { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal DiscountPercent { get; init; }
    public decimal LineTotal { get; init; }
    public IReadOnlyList<QuotationProductPricingTierPreviewDto> PriceTiers { get; init; } = [];
    public string? Note { get; init; }
    public int SortOrder { get; init; }
}

public sealed class QuotationApprovedTierPricingDto
{
    public string Currency { get; init; } = string.Empty;
    public Guid ProductPricingVersionId { get; init; }
    public int Version { get; init; }
    public decimal? StandardSellingPrice { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ProductPricingStatus Status { get; init; }

    public DateTime? ApprovedAt { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ProductPricingSourceType SourceType { get; init; }

    public Guid SourceId { get; init; }
    public string SourceExternalId { get; init; } = string.Empty;
    public string SourceName { get; init; } = string.Empty;
    public IReadOnlyList<QuotationReferencePriceTierDto> PriceTiers { get; init; } = [];
}

public sealed class QuotationSystemCalculatedTierPricingDto
{
    public string PricingStatus { get; init; } = string.Empty;
    public DateTime CalculatedAt { get; init; }
    public decimal? StandardSellingPrice { get; init; }
    public string Currency { get; init; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ProductPricingSourceType? SourceType { get; init; }

    public Guid? SourceId { get; init; }
    public string SourceExternalId { get; init; } = string.Empty;
    public string SourceName { get; init; } = string.Empty;
    public IReadOnlyList<QuotationReferencePriceTierDto> PriceTiers { get; init; } = [];
}

public sealed class QuotationLatestQuotedTierPricingDto
{
    public string Currency { get; init; } = string.Empty;
    public Guid QuotationId { get; init; }
    public string QuotationExternalId { get; init; } = string.Empty;
    public DateTime QuotationDate { get; init; }
    public DateTime SentDate { get; init; }
    public IReadOnlyList<QuotationReferencePriceTierDto> PriceTiers { get; init; } = [];
}

public sealed class QuotationReferencePriceTierDto
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

/// <summary>
/// Tier giá preview chưa được lưu; vì vậy không có QuotationLinePriceTierId.
/// </summary>
public sealed class QuotationProductPricingTierPreviewDto
{
    public bool IsSnapshot { get; init; }
    public bool RequiresManualPrice { get; init; }
    public string QuantityRangeLabel { get; init; } = string.Empty;
    public decimal? MinQuantity { get; init; }
    public decimal? MaxQuantity { get; init; }
    public bool MinInclusive { get; init; }
    public bool MaxInclusive { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal CommissionAmount { get; init; }
    public decimal CustomerUnitPrice { get; init; }
    public decimal? StandardUnitPrice { get; init; }
    public DateTime? StandardPriceUpdatedDate { get; init; }
    public decimal? LatestQuotedUnitPrice { get; init; }
    public DateTime? LatestQuotedDate { get; init; }
    public int SortOrder { get; init; }
}
