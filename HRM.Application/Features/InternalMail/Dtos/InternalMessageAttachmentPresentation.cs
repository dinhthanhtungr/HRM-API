namespace HRM.Application.Features.InternalMail.Dtos;

internal static class InternalMessageAttachmentPresentation
{
    private const string ContentRoute = "/api/v1/internal-mail/attachments";

    public static void Enrich(InternalMessageAttachmentDto attachment)
    {
        (attachment.ContentType, attachment.Kind, attachment.IsImage) = Resolve(attachment.FileName);
        attachment.MediaKind = ResolveMediaKind(attachment.ContentType);
        attachment.ContentUrl = $"{ContentRoute}/{attachment.AttachmentId}";
        attachment.ThumbnailUrl = attachment.IsImage
            ? $"{attachment.ContentUrl}/thumbnail"
            : null;
        attachment.DownloadUrl = $"{attachment.ContentUrl}?mode=download";
        if (attachment.MediaKind == HRM.Domain.Enums.Attachment.AttachmentMediaKind.Video)
        {
            attachment.VideoInfoUrl = $"{attachment.ContentUrl}/video/info";
            attachment.VideoRetryUrl = $"{attachment.ContentUrl}/video/retry";
            attachment.PlaybackUrl = $"{attachment.ContentUrl}/video/playback";
            attachment.PosterUrl = $"{attachment.ContentUrl}/video/poster";
        }
    }

    public static void Enrich(InternalConversationAttachmentDto attachment, Guid? conversationId = null)
    {
        (attachment.ContentType, attachment.Kind, attachment.IsImage) = Resolve(attachment.FileName);
        attachment.MediaKind = ResolveMediaKind(attachment.ContentType);
        attachment.ContentUrl = string.Equals(attachment.Source, "SampleRequest", StringComparison.Ordinal)
            && conversationId.HasValue
            ? $"/api/v1/internal-mail/conversations/{conversationId.Value}/related-attachments/{attachment.AttachmentId}"
            : $"{ContentRoute}/{attachment.AttachmentId}";
        attachment.ThumbnailUrl = attachment.IsImage
            ? $"{attachment.ContentUrl}/thumbnail"
            : null;
        attachment.DownloadUrl = $"{attachment.ContentUrl}?mode=download";
        if (attachment.MediaKind == HRM.Domain.Enums.Attachment.AttachmentMediaKind.Video)
        {
            attachment.VideoInfoUrl = $"{attachment.ContentUrl}/video/info";
            attachment.VideoRetryUrl = $"{attachment.ContentUrl}/video/retry";
            attachment.PlaybackUrl = $"{attachment.ContentUrl}/video/playback";
            attachment.PosterUrl = $"{attachment.ContentUrl}/video/poster";
        }
    }

    internal static HRM.Domain.Enums.Attachment.AttachmentMediaKind ResolveMediaKind(string contentType)
        => contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase)
            ? HRM.Domain.Enums.Attachment.AttachmentMediaKind.Video
            : contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
                ? HRM.Domain.Enums.Attachment.AttachmentMediaKind.Image
                : contentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase)
                    ? HRM.Domain.Enums.Attachment.AttachmentMediaKind.Audio
                    : HRM.Domain.Enums.Attachment.AttachmentMediaKind.Document;

    private static (string ContentType, string Kind, bool IsImage) Resolve(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".png" => ("image/png", "Image", true),
            ".jpg" or ".jpeg" => ("image/jpeg", "Image", true),
            ".gif" => ("image/gif", "Image", true),
            ".webp" => ("image/webp", "Image", true),
            ".bmp" => ("image/bmp", "Image", true),
            ".mp4" or ".m4v" => ("video/mp4", "Video", false),
            ".mov" => ("video/quicktime", "Video", false),
            ".webm" => ("video/webm", "Video", false),
            ".avi" => ("video/x-msvideo", "Video", false),
            ".mkv" => ("video/x-matroska", "Video", false),
            ".3gp" => ("video/3gpp", "Video", false),
            ".mp3" => ("audio/mpeg", "Audio", false),
            ".m4a" => ("audio/mp4", "Audio", false),
            ".wav" => ("audio/wav", "Audio", false),
            ".pdf" => ("application/pdf", "Pdf", false),
            ".doc" => ("application/msword", "Document", false),
            ".docx" => ("application/vnd.openxmlformats-officedocument.wordprocessingml.document", "Document", false),
            ".xls" => ("application/vnd.ms-excel", "Spreadsheet", false),
            ".xlsx" => ("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Spreadsheet", false),
            ".txt" => ("text/plain", "Text", false),
            ".csv" => ("text/csv", "Spreadsheet", false),
            ".zip" => ("application/zip", "Archive", false),
            _ => ("application/octet-stream", "File", false)
        };
    }
}
