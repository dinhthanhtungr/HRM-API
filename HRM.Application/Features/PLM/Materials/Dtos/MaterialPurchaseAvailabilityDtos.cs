using System.Text.Json.Serialization;
using HRM.Domain.Enums.Materials;

namespace HRM.Application.Features.PLM.Materials.Dtos;

public sealed class UpdateMaterialPurchaseAvailabilityRequest
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public MaterialPurchaseStatus Status { get; init; }

    public string? Reason { get; init; }

    public DateTime? EffectiveFrom { get; init; }

    public DateTime? ExpectedAvailableDate { get; init; }

    public string? Note { get; init; }
}

public sealed class MaterialPurchaseAvailabilityDto
{
    public Guid MaterialId { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public MaterialPurchaseStatus Status { get; init; }

    public string? Reason { get; init; }
    public DateTime? EffectiveFrom { get; init; }
    public DateTime? ExpectedAvailableDate { get; init; }
    public string? Note { get; init; }
    public Guid? UpdatedBy { get; init; }
    public DateTime? UpdatedDate { get; init; }
    public bool NotificationPublished { get; init; }
}

public enum UpdateMaterialPurchaseAvailabilityOutcome
{
    Updated,
    NotFound,
    InvalidRequest
}

public sealed record UpdateMaterialPurchaseAvailabilityResult(
    UpdateMaterialPurchaseAvailabilityOutcome Outcome,
    MaterialPurchaseAvailabilityDto? Data = null,
    string? Message = null);
