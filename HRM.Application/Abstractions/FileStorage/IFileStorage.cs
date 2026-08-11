namespace HRM.Application.Abstractions.FileStorage;

public interface IFileStorage
{
    Task<string> SaveAsync(
        Stream stream,
        string contentType,
        string fileName,
        string relativeFolder,
        CancellationToken cancellationToken = default);

    Task SaveAtPathAsync(
        Stream stream,
        string relativePath,
        CancellationToken cancellationToken = default);

    Task<StoredFile> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default);

    Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default);
}

public sealed record StoredFile(
    Stream Stream,
    string ContentType,
    string FileName,
    long Length);
