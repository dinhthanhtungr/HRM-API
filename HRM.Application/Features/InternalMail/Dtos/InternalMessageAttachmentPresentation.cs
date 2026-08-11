namespace HRM.Application.Features.InternalMail.Dtos;

internal static class InternalMessageAttachmentPresentation
{
    private const string ContentRoute = "/api/v1/internal-mail/attachments";

    public static void Enrich(InternalMessageAttachmentDto attachment)
    {
        (attachment.ContentType, attachment.Kind, attachment.IsImage) = Resolve(attachment.FileName);
        attachment.ContentUrl = $"{ContentRoute}/{attachment.AttachmentId}";
        attachment.ThumbnailUrl = attachment.IsImage
            ? $"{attachment.ContentUrl}/thumbnail"
            : null;
        attachment.DownloadUrl = $"{attachment.ContentUrl}?mode=download";
    }

    public static void Enrich(InternalConversationAttachmentDto attachment)
    {
        (attachment.ContentType, attachment.Kind, attachment.IsImage) = Resolve(attachment.FileName);
        attachment.ContentUrl = $"{ContentRoute}/{attachment.AttachmentId}";
        attachment.ThumbnailUrl = attachment.IsImage
            ? $"{attachment.ContentUrl}/thumbnail"
            : null;
        attachment.DownloadUrl = $"{attachment.ContentUrl}?mode=download";
    }

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
