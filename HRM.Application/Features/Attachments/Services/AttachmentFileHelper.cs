namespace HRM.Application.Features.Attachments.Services;

internal static class AttachmentFileHelper
{
    private const string AttachmentContentRoute = "/api/v1/attachments";

    public static string BuildUrl(Guid attachmentId)
    {
        return $"{AttachmentContentRoute}/{attachmentId}";
    }

    public static string BuildDownloadUrl(Guid attachmentId)
    {
        return $"{BuildUrl(attachmentId)}?mode=download";
    }

    public static bool IsImageFile(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        var extension = Path.GetExtension(fileName);

        return extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".gif", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".webp", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase);
    }
}
