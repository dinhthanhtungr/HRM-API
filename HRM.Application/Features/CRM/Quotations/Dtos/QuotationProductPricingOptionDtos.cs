using System.Text.Json.Serialization;
using HRM.Application.Commons.Pricing.Dtos;
using HRM.Domain.Enums.Formulas;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Features.CRM.Quotations.Dtos;

public sealed class QuotationProductPricingOptionDto
{
    public Guid? SampleRequestId { get; init; }
    public string? SampleRequestExternalId { get; init; }
    public DateTime? CompletedDate { get; init; }
    public bool HasSampleRequest { get; init; }

    public Guid ProductId { get; init; }
    public string ProductCode { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ProductPricingLookupStatus PricingStatus { get; init; }

    public bool HasPricingVersion { get; init; }
    public ProductPricingVersionDto? CurrentPricing { get; init; }

    public Guid? CustomerId { get; init; }
    public string? CustomerName { get; init; }
    public string? CustomerExternalId { get; init; }

    public bool HasFormula { get; init; }
    public bool HasEligiblePricingSource { get; init; }
    public IReadOnlyList<ProductPricingSourceOptionDto> PricingSources { get; init; } = [];
    public IReadOnlyList<QuotationProductPricingFormulaDto> Formulas { get; init; } = [];
}

public sealed class QuotationProductPricingFormulaDto
{
    public Guid FormulaId { get; init; }
    public string FormulaExternalId { get; init; } = string.Empty;
    public string FormulaName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public bool IsCustomerSelected { get; init; }

    public decimal? MaterialCost { get; init; }
    public decimal? RealtimeMaterialCost { get; init; }
    public bool? IsRealtimeMaterialCostComplete { get; init; }
    public int? MissingMaterialPriceCount { get; init; }

    public decimal? ManufacturingCost { get; init; }
    public decimal? StandardSellingPrice { get; init; }
    public decimal? ProfitMarginRate { get; init; }

    public DateTime? PricingUpdatedDate { get; init; }
    public FormulaPriceCalculationDto? Pricing { get; init; }

    public IReadOnlyList<QuotationProductPricingMaterialDto>? Materials { get; init; }
}

public sealed class QuotationProductPricingMaterialDto
{
    public Guid FormulaMaterialId { get; init; }
    public Guid? ItemId { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ItemType ItemType { get; init; }

    public string ItemCode { get; init; } = string.Empty;
    public string ItemName { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public string Unit { get; init; } = string.Empty;

    public Guid? CategoryId { get; init; }
    public bool HasLatestPrice { get; init; }
    public decimal? LatestUnitPrice { get; init; }
    public decimal? LatestTotalPrice { get; init; }
    public DateTime? LatestPriceDate { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LatestPriceSourceType LatestPriceSource { get; init; } = LatestPriceSourceType.Unknown;

    public IReadOnlyList<QuotationProductPricingMaterialSupplierDto> SupplierPrices { get; init; } = [];
}

public sealed class QuotationProductPricingMaterialSupplierDto
{
    public Guid MaterialsSupplierId { get; init; }
    public Guid SupplierId { get; init; }
    public string SupplierCode { get; init; } = string.Empty;
    public string SupplierName { get; init; } = string.Empty;
    public decimal? CurrentPrice { get; init; }
    public string? Currency { get; init; }
    public bool IsPreferred { get; init; }
    public DateTime? UpdatedDate { get; init; }
}
