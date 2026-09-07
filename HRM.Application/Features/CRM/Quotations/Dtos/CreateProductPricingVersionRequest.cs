using System.Text.Json.Serialization;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Features.CRM.Quotations.Dtos;

public sealed class CreateProductPricingVersionRequest
{
    public Guid ProductId { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ProductPricingSourceType SourceType { get; init; }

    public Guid SourceId { get; init; }
    public string Currency { get; init; } = string.Empty;
    public bool ApproveImmediately { get; init; }
    public decimal? MaterialCostSnapshot { get; init; }
    public decimal? ManufacturingCost { get; init; }
    public decimal? StandardSellingPrice { get; init; }
    public decimal? ProfitMarginRate { get; init; }
    public string? Note { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ProductPricingChangedField? ChangedField { get; init; }

    public DateTime? CalculatedAt { get; init; }
    public IReadOnlyList<ProductPricingTierRequest> PriceTiers { get; init; } = [];
}
