using System.Text.Json.Serialization;
using HRM.Domain.Enums.Attachment;

namespace HRM.Application.Features.PLM.Materials.DocumentImport.Dtos;

public sealed class MaterialDocumentImportPreviewDto
{
    public string SourceLabel { get; init; } = string.Empty;
    public string SourceStatus { get; init; } = string.Empty;
    public string? ErrorCode { get; init; }
    public bool IsTruncated { get; init; }
    public MaterialDocumentImportPreviewSummaryDto Summary { get; init; } = new();
    public IReadOnlyList<MaterialDocumentImportPreviewItemDto> Items { get; init; } = [];
}

public sealed class MaterialDocumentImportPreviewSummaryDto
{
    public int TotalFiles { get; init; }
    public int ExactMatches { get; init; }
    public int AmbiguousMatches { get; init; }
    public int UnmatchedFiles { get; init; }
    public int RequiresReview { get; init; }
}

public sealed class MaterialDocumentImportPreviewItemDto
{
    public string RelativePath { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public DateTime LastWriteTime { get; init; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AttachmentSlot Slot { get; init; } = AttachmentSlot.MaterialOther;
    public string MatchStatus { get; init; } = string.Empty;
    public bool RequiresReview { get; init; }
    public IReadOnlyList<string> DetectedMaterialCodes { get; init; } = [];
    public Guid? MaterialId { get; init; }
    public string? MaterialExternalId { get; init; }
    public string? MaterialName { get; init; }
    public IReadOnlyList<string> Notes { get; init; } = [];
}
