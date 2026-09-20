using System.Text.Json.Serialization;
using HRM.Domain.Enums.Materials;

namespace HRM.Application.Features.PLM.Materials.Dtos;

/// <summary>
/// Canonical read contract for a material's purchase availability.
/// </summary>
public sealed class MaterialPurchaseAvailabilityInfoDto
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public MaterialPurchaseStatus Status { get; init; }

    public bool IsPurchaseAvailable { get; init; }
    public string? Reason { get; init; }
    public DateTime? EffectiveFrom { get; init; }
    public DateTime? ExpectedAvailableDate { get; init; }
}
