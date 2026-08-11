namespace HRM.Application.Abstractions.FileStorage;

public interface IImageThumbnailGenerator
{
    Task<Stream?> GenerateWebpAsync(
        Stream source,
        int maxWidth,
        int maxHeight,
        CancellationToken cancellationToken = default);
}
