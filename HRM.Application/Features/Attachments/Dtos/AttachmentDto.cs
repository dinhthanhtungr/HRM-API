using HRM.Domain.Enums.Attachment;

namespace HRM.Application.Features.Attachments.Dtos;

public sealed class AttachmentDto
{
    public Guid AttachmentId { get; set; }
    public Guid AttachmentCollectionId { get; set; }
    public AttachmentSlot Slot { get; set; }
    public string FileName { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string Url { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public bool IsImage { get; set; }
    public DateTime CreateDate { get; set; }
    public Guid? CreateBy { get; set; }
}

public sealed record AttachmentUploadFile(
    Stream Stream,
    string FileName,
    string ContentType,
    long Length);

public sealed record AttachmentContent(
    Stream Stream,
    string ContentType,
    string FileName,
    long Length);
