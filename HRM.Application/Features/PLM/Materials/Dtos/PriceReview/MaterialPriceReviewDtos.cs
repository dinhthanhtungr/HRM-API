using System.Text.Json.Serialization;
using HRM.Application.Commons.Pricing.Models;
using HRM.Domain.Enums.Materials;

namespace HRM.Application.Features.PLM.Materials.Dtos.PriceReview;

public enum MaterialPriceReviewUsageSource
{
    RecentSampleRequestFormula = 0,
    ManufacturingFormula = 1
}

public enum MaterialPriceReviewStatus
{
    MissingPrice = 0,
    NeedsReview = 1,
    UpToDate = 2
}

public sealed class MaterialPriceReviewItemDto
{
    public Guid MaterialId { get; init; }
    public string? ExternalId { get; init; }
    public string? CustomCode { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Type { get; init; }
    public string? Unit { get; init; }
    public decimal? CurrentPrice { get; init; }
    public DateTime? LastPriceUpdatedAt { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public MaterialPriceSource? PriceSource { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public MaterialPriceReviewStatus? ReviewStatus { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public MaterialPriceReviewUsageSource? UsageSource { get; init; }

    public DateTime? LastUsedAt { get; init; }
    public int? SupplierCount { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public MaterialPurchaseStatus PurchaseStatus { get; init; }

    public string? PurchaseStatusReason { get; init; }
    public DateTime? PurchaseStatusEffectiveFrom { get; init; }
    public DateTime? ExpectedAvailableDate { get; init; }
}

public sealed class MaterialPriceReviewSuppliersDto
{
    public Guid MaterialId { get; init; }
    public IReadOnlyList<MaterialPriceReviewSupplierDto> Suppliers { get; init; } = [];
}

public sealed class MaterialPriceReviewSupplierDto
{
    public Guid MaterialsSupplierId { get; init; }
    public Guid SupplierId { get; init; }
    public string SupplierCode { get; init; } = string.Empty;
    public string SupplierName { get; init; } = string.Empty;
    public decimal? CurrentPrice { get; init; }
    public string? Currency { get; init; }
    public bool IsPreferred { get; init; }
    public DateTime? LastPriceUpdatedAt { get; init; }
}
