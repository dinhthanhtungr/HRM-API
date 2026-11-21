namespace HRM.Application.Abstractions.FileStorage;

public sealed record VideoMediaInfo(string Status, double? DurationSeconds = null,
    int? Width = null, int? Height = null);

public interface IVideoMediaService
{
    Task QueueAsync(Guid attachmentId, string storagePath, CancellationToken cancellationToken);
    Task<VideoMediaInfo> GetInfoAsync(Guid attachmentId, CancellationToken cancellationToken);
    Task<StoredFile?> OpenAsync(Guid attachmentId, bool poster, CancellationToken cancellationToken);
    Task RetryAsync(Guid attachmentId, CancellationToken cancellationToken);
}
