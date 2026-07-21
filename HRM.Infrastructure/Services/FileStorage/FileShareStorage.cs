using HRM.Application.Abstractions.FileStorage;
using Microsoft.Extensions.Options;

namespace HRM.Infrastructure.Services.FileStorage;

internal sealed class FileShareStorage : IFileStorage
{
    private readonly StorageOptions _options;

    public FileShareStorage(IOptions<StorageOptions> options)
    {
        _options = options.Value;

        if (string.IsNullOrWhiteSpace(_options.RootPath))
        {
            throw new InvalidOperationException("Storage:RootPath is missing.");
        }
    }

    public async Task<string> SaveAsync(
        Stream stream,
        string contentType,
        string fileName,
        string relativeFolder,
        CancellationToken cancellationToken = default)
    {
        var folder = ResolvePath(relativeFolder);
        Directory.CreateDirectory(folder);

        var safeName = Path.GetFileName(fileName);
        var key = $"{DateTime.Now:yyyyMMddHHmmssfff}_{Guid.CreateVersion7():N}_{safeName}";
        var fullPath = ResolvePath(Path.Combine(relativeFolder, key));

        await using var fileStream = new FileStream(
            fullPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.Read,
            81920,
            useAsync: true);

        await stream.CopyToAsync(fileStream, cancellationToken);

        return Path.Combine(relativeFolder, key).Replace('\\', '/');
    }

    public Task<StoredFile> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolvePath(relativePath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Stored file was not found.", relativePath);
        }

        var stream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            useAsync: true);

        var fileName = Path.GetFileName(fullPath);
        var contentType = GetContentType(fileName);

        return Task.FromResult(new StoredFile(stream, contentType, fileName, stream.Length));
    }

    public Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolvePath(relativePath);

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    private string ResolvePath(string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
        {
            throw new InvalidOperationException("Storage path must be relative.");
        }

        var root = Path.GetFullPath(_options.RootPath);
        var fullPath = Path.GetFullPath(Path.Combine(root, relativePath));

        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Storage path escapes the configured root.");
        }

        return fullPath;
    }

    private static string GetContentType(string fileName)
    {
        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".txt" => "text/plain",
            ".csv" => "text/csv",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".zip" => "application/zip",
            _ => "application/octet-stream"
        };
    }
}
