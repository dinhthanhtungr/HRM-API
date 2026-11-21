namespace HRM.Domain.Security.Rules.Attachment;

public static class AttachmentMediaRules
{
    public static bool IsVideoFile(string fileName) => Path.GetExtension(fileName).ToLowerInvariant()
        is ".mp4" or ".m4v" or ".mov" or ".webm" or ".avi" or ".mkv" or ".3gp";
}
