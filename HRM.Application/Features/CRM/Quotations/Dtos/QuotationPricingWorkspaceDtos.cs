using System.Text.Json.Serialization;
using HRM.Application.Commons.Pricing.Dtos;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Features.CRM.Quotations.Dtos;

public sealed class QuotationPricingWorkspaceDto
{
    public Guid QuotationId { get; init; }
    public string QuotationExternalId { get; init; } = string.Empty;
    public Guid CustomerId { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public Guid SaleEmployeeId { get; init; }
    public string SaleEmployeeName { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;
    public DateTime QuotationDate { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public QuotationStatus Status { get; init; }

    public IReadOnlyList<QuotationPricingWorkspaceLineDto> Lines { get; init; } = [];
}

public sealed class QuotationPricingWorkspaceLineDto
{
    public Guid QuotationLineId { get; init; }
    public Guid ProductId { get; init; }
    public string ProductCode { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public string Unit { get; init; } = string.Empty;
    public int SortOrder { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public QuotationPricingWorkspaceState PricingState { get; init; }

    public Guid? AppliedProductPricingVersionId { get; init; }
    public decimal AppliedUnitPrice { get; init; }
    public bool HasNewerApprovedPricing { get; init; }
    public IReadOnlyList<QuotationLinePriceTierDto> AppliedPriceTiers { get; init; } = [];

    public ProductPricingVersionDto? DraftPricing { get; init; }
    public ProductPricingVersionDto? ApprovedPricing { get; init; }

    public ProductPricingSourceOptionDto? SelectedPricingSource { get; init; }
    public decimal? CurrentMaterialCost { get; init; }
    public bool IsCurrentMaterialCostComplete { get; init; }
    public int MissingMaterialPriceCount { get; init; }
    public decimal? StoredMaterialCostSnapshot { get; init; }
    public DateTime? StoredPricingUpdatedDate { get; init; }
    public FormulaPriceCalculationDto? EffectivePricing { get; init; }
    public bool PriceTiersAreStored { get; init; }
    public IReadOnlyList<QuotationPricingWorkspaceTierDto> DisplayPriceTiers { get; init; } = [];
    public IReadOnlyList<ProductPricingSourceOptionDto> PricingSources { get; init; } = [];
}

public sealed class QuotationPricingWorkspaceTierDto
{
    public string QuantityRangeLabel { get; init; } = string.Empty;
    public decimal? MinQuantity { get; init; }
    public decimal? MaxQuantity { get; init; }
    public bool MinInclusive { get; init; }
    public bool MaxInclusive { get; init; }
    public decimal? UnitPrice { get; init; }
    public decimal? MarginVsMaterialPercent { get; init; }
    public decimal? MarginVsCostPercent { get; init; }
    public bool RequiresManualPrice { get; init; }
    public bool IsStored { get; init; }
    public int SortOrder { get; init; }
}

public sealed class QuotationPricingQueueItemDto
{
    public Guid QuotationId { get; init; }
    public string QuotationExternalId { get; init; } = string.Empty;
    public Guid CustomerId { get; init; }
    public string CustomerExternalId { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public Guid SaleEmployeeId { get; init; }
    public string SaleEmployeeName { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;
    public DateTime QuotationDate { get; init; }
    public DateTime RequestedAt { get; init; }
    public int LineCount { get; init; }
    public int PendingPricingLineCount { get; init; }
    public IReadOnlyList<string> ProductCodes { get; init; } = [];
}
