using System.Text.Json;
using HRM.Application.Features.PLM.Materials.Dtos;

namespace HRM.Application.Features.PLM.Materials.Commands;

internal static class MaterialReplacementWriteSupport
{
    internal const int MaxTechnicalNoteLength = 2_000;

    internal static string? Validate(
        Guid replacementMaterialId,
        JsonElement? applicableContext,
        string? technicalNote,
        decimal? replacementRatio,
        int priority)
    {
        if (replacementMaterialId == Guid.Empty)
        {
            return "ReplacementMaterialId is required.";
        }

        if (replacementRatio is <= 0)
        {
            return "ReplacementRatio must be greater than zero when provided.";
        }

        if (priority <= 0)
        {
            return "Priority must be greater than zero.";
        }

        if (Normalize(technicalNote)?.Length > MaxTechnicalNoteLength)
        {
            return $"TechnicalNote must not exceed {MaxTechnicalNoteLength} characters.";
        }

        if (applicableContext.HasValue && applicableContext.Value.ValueKind != JsonValueKind.Object)
        {
            return "ApplicableContext must be a JSON object.";
        }

        return null;
    }

    internal static JsonDocument ToDocument(JsonElement? applicableContext)
        => applicableContext.HasValue
            ? JsonDocument.Parse(applicableContext.Value.GetRawText())
            : JsonDocument.Parse("{}");

    internal static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    internal static MaterialReplacementDto ToDto(MaterialReplacementProjection row)
        => new()
        {
            MaterialReplacementId = row.MaterialReplacementId,
            SourceMaterialId = row.SourceMaterialId,
            ReplacementMaterialId = row.ReplacementMaterialId,
            ExternalId = row.ExternalId,
            CustomCode = row.CustomCode,
            Name = row.Name,
            PurchaseStatus = row.PurchaseStatus,
            IsPurchaseAvailable = row.PurchaseStatus != HRM.Domain.Enums.Materials.MaterialPurchaseStatus.Unavailable,
            ApplicableContext = row.ApplicableContext.RootElement.Clone(),
            TechnicalNote = row.TechnicalNote,
            ReplacementRatio = row.ReplacementRatio,
            Priority = row.Priority,
            IsRecommended = row.IsRecommended,
            IsActive = row.IsActive,
            CreatedDate = row.CreatedDate,
            UpdatedDate = row.UpdatedDate
        };
}

internal sealed class MaterialReplacementProjection
{
    public Guid MaterialReplacementId { get; init; }
    public Guid SourceMaterialId { get; init; }
    public Guid ReplacementMaterialId { get; init; }
    public string? ExternalId { get; init; }
    public string? CustomCode { get; init; }
    public string? Name { get; init; }
    public HRM.Domain.Enums.Materials.MaterialPurchaseStatus PurchaseStatus { get; init; }
    public JsonDocument ApplicableContext { get; init; } = JsonDocument.Parse("{}");
    public string? TechnicalNote { get; init; }
    public decimal? ReplacementRatio { get; init; }
    public int Priority { get; init; }
    public bool IsRecommended { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedDate { get; init; }
    public DateTime? UpdatedDate { get; init; }
}
