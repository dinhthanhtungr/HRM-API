namespace HRM.Application.Features.PLM.Formulas.Dtos.RelatedAttachments;

public sealed class FormulaRelatedAttachmentsDto
{
    public Guid FormulaId { get; init; }
    public int TotalCount { get; init; }
    public IReadOnlyList<FormulaRelatedAttachmentGroupDto> Groups { get; init; } = [];
}

public sealed class FormulaRelatedAttachmentGroupDto
{
    public string SourceType { get; init; } = "Material";
    public Guid SourceId { get; init; }
    public string? SourceExternalId { get; init; }
    public string? SourceName { get; init; }
    public IReadOnlyList<FormulaRelatedAttachmentDto> Attachments { get; init; } = [];
}

public sealed class FormulaRelatedAttachmentDto
{
    public Guid AttachmentId { get; init; }
    public string FileName { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public string ContentType { get; init; } = "application/octet-stream";
    public bool IsImage { get; init; }
    public bool IsPdf { get; init; }
    public string ContentUrl { get; init; } = string.Empty;
    public string DownloadUrl { get; init; } = string.Empty;
    public DateTime CreatedDate { get; init; }
}
