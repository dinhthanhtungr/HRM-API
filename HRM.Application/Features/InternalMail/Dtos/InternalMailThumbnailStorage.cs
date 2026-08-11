namespace HRM.Application.Features.InternalMail.Dtos;

internal static class InternalMailThumbnailStorage
{
    public const int MaxWidth = 480;
    public const int MaxHeight = 480;

    public static string GetPath(string originalStoragePath, Guid attachmentId)
    {
        var directory = Path.GetDirectoryName(originalStoragePath)?.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException("Attachment storage path does not contain a directory.");
        }

        return $"{directory}/thumbnails/{attachmentId:N}.webp";
    }
}
