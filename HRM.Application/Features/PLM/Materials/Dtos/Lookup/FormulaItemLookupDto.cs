using System.Text.Json.Serialization;
using HRM.Application.Features.PLM.Formulas.Dtos.Commons;
using HRM.Domain.Enums.Formulas;
using HRM.Domain.Enums.Materials;

namespace HRM.Application.Features.PLM.Materials.Dtos.Lookup;

/// <summary>
/// Một lựa chọn NVL hoặc Product dùng khi lên công thức.
/// </summary>
public sealed class FormulaItemLookupDto
{
    public Guid ItemId { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ItemType ItemType { get; init; }

    public string? ExternalId { get; init; }
    public string? CustomCode { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public Guid? CategoryId { get; init; }
    public string? CategoryCode { get; init; }
    public double? Weight { get; init; }
    public string? Package { get; init; }
    public string? Unit { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public MaterialPurchaseStatus? PurchaseStatus { get; init; }

    public bool? IsPurchaseAvailable { get; init; }
    public string? PurchaseStatusReason { get; init; }
    public DateTime? PurchaseStatusEffectiveFrom { get; init; }
    public DateTime? ExpectedAvailableDate { get; init; }

    public LatestPriceSource Price { get; init; } = new();
}
