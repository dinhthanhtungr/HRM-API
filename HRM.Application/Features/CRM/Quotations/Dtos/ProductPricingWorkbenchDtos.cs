using System.Text.Json.Serialization;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Features.CRM.Quotations.Dtos;

public enum ProductPricingWorkbenchView
{
    NeedsPricing = 0,
    Draft = 10,
    Approved = 20,
    All = 30
}

public sealed class ProductPricingWorkbenchItemDto
{
    public bool CanOpenPricingDetail { get; init; }
    public bool CanManagePricing { get; init; }
    public Guid ProductId { get; init; }
    public string ProductCode { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ProductPricingLookupStatus PricingStatus { get; init; }
    public bool IsSystemCalculatedDraft { get; init; }

    public int WaitingQuotationCount { get; init; }
    public DateTime? LatestRequestedAt { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ProductPricingSourceType? SourceType { get; init; }

    public Guid? SourceId { get; init; }
    public string? SourceExternalId { get; init; }
    public string? SourceName { get; init; }
    public string? SourceStatus { get; init; }
    public bool SourceIsEligible { get; init; }
    public bool SourceIsCustomerSelected { get; init; }

    public decimal? CurrentMaterialCost { get; init; }
    public bool IsCurrentMaterialCostComplete { get; init; }
    public int MissingMaterialPriceCount { get; init; }
    public decimal? StoredMaterialCostSnapshot { get; init; }
    public decimal? MaterialCostDifference { get; init; }
    public decimal? MaterialCostDifferencePercent { get; init; }

    public decimal? ManufacturingCost { get; init; }
    public bool UsedDefaultManufacturingCost { get; init; }
    public decimal? StandardSellingPrice { get; init; }
    public decimal? ProfitMarginRate { get; init; }

    public Guid? DraftPricingVersionId { get; init; }
    public Guid? ApprovedPricingVersionId { get; init; }
    public DateTime? PricingUpdatedDate { get; init; }
}

public sealed class ProductPricingWorkbenchDetailDto
{
    public ProductPricingWorkbenchItemDto Summary { get; init; } = new();
    public decimal? ManufacturingCost { get; init; }
    public decimal? StandardSellingPrice { get; init; }
    public decimal? ProfitMarginRate { get; init; }
    public ProductPricingSourceOptionDto? SelectedSource { get; init; }
    public ProductPricingVersionDto? DraftPricing { get; init; }
    public ProductPricingVersionDto? ApprovedPricing { get; init; }
    public IReadOnlyList<QuotationPricingWorkspaceTierDto> DisplayPriceTiers { get; init; } = [];
    public IReadOnlyList<ProductPricingVersionDto> PricingHistory { get; init; } = [];
    public IReadOnlyList<ProductPricingRelatedQuotationDto> RelatedQuotations { get; init; } = [];
}

public sealed class ProductPricingRelatedQuotationDto
{
    public Guid QuotationId { get; init; }
    public string QuotationExternalId { get; init; } = string.Empty;
    public Guid CustomerId { get; init; }
    public string CustomerExternalId { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public Guid SaleEmployeeId { get; init; }
    public string SaleEmployeeName { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public string Unit { get; init; } = string.Empty;
    public DateTime RequestedAt { get; init; }
}
