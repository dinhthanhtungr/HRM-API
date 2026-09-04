using HRM.Application.Abstractions.FileStorage;
using Microsoft.Extensions.Options;

namespace HRM.Infrastructure.Services.FileStorage;

internal sealed class FileShareMaterialDocumentSourceScanner
    : IMaterialDocumentSourceScanner
{
    private readonly MaterialDocumentImportOptions _options;

    public FileShareMaterialDocumentSourceScanner(
        IOptions<MaterialDocumentImportOptions> options)
    {
        _options = options.Value;
    }

    public Task<MaterialDocumentSourceScan> ScanAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Scan(cancellationToken), cancellationToken);
    }

    public Task<MaterialDocumentSourceContent> OpenReadAsync(
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fullPath = ResolveSourceFilePath(relativePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Material document source file was not found.");
        }

        var stream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            useAsync: true);

        return Task.FromResult(new MaterialDocumentSourceContent(
            stream,
            Path.GetFileName(fullPath),
            GetContentType(fullPath),
            stream.Length));
    }

    private MaterialDocumentSourceScan Scan(CancellationToken cancellationToken)
    {
        var sourceLabel = string.IsNullOrWhiteSpace(_options.SourceLabel)
            ? "TDS/MSDS NVL"
            : _options.SourceLabel.Trim();
        var configuredSourceRoots = GetConfiguredSourceRoots();
        if (configuredSourceRoots.Count == 0)
        {
            return Unavailable(sourceLabel, false, "source_roots_not_configured");
        }

        var sourceRoots = new List<string>(configuredSourceRoots.Count);
        foreach (var configuredSourceRoot in configuredSourceRoots)
        {
            try
            {
                if (!Path.IsPathFullyQualified(configuredSourceRoot))
                {
                    return Unavailable(sourceLabel, true, "source_root_must_be_absolute");
                }

                var sourceRoot = Path.GetFullPath(configuredSourceRoot);
                if (!Directory.Exists(sourceRoot))
                {
                    return Unavailable(sourceLabel, true, "source_root_not_found");
                }

                sourceRoots.Add(sourceRoot);
            }
            catch (Exception exception) when (
                exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return Unavailable(sourceLabel, true, "source_root_invalid");
            }
        }

        var allowedExtensions = (_options.AllowedExtensions ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(NormalizeExtension)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var maxFiles = Math.Clamp(_options.MaxFiles, 1, 50_000);
        var files = new List<MaterialDocumentSourceFile>(Math.Min(maxFiles, 1_000));

        try
        {
            var enumerationOptions = new EnumerationOptions
            {
                RecurseSubdirectories = _options.IncludeSubdirectories,
                IgnoreInaccessible = false,
                ReturnSpecialDirectories = false,
                AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.System
            };

            for (var sourceIndex = 0; sourceIndex < sourceRoots.Count; sourceIndex++)
            {
                var sourceRoot = sourceRoots[sourceIndex];
                foreach (var path in Directory.EnumerateFiles(sourceRoot, "*", enumerationOptions))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (allowedExtensions.Count > 0 &&
                        !allowedExtensions.Contains(Path.GetExtension(path)))
                    {
                        continue;
                    }

                    if (files.Count == maxFiles)
                    {
                        return Available(sourceLabel, true, files);
                    }

                    var relativePath = Path.GetRelativePath(sourceRoot, path).Replace('\\', '/');
                    files.Add(new MaterialDocumentSourceFile(
                        sourceRoots.Count == 1 ? relativePath : $"{sourceIndex}/{relativePath}",
                        new FileInfo(path).Name,
                        new FileInfo(path).Length,
                        new FileInfo(path).LastWriteTime));
                }
            }

            return Available(sourceLabel, false, files);
        }
        catch (UnauthorizedAccessException)
        {
            return Unavailable(sourceLabel, true, "source_access_denied");
        }
        catch (IOException)
        {
            return Unavailable(sourceLabel, true, "source_io_error");
        }
    }

    private static string NormalizeExtension(string extension)
    {
        var trimmed = extension.Trim();
        return trimmed.StartsWith('.') ? trimmed : $".{trimmed}";
    }

    private string ResolveSourceFilePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) ||
            Path.IsPathFullyQualified(relativePath))
        {
            throw new InvalidOperationException("Material document source path is invalid.");
        }

        var sourceRoots = GetConfiguredSourceRoots();
        if (sourceRoots.Count == 0)
        {
            throw new InvalidOperationException("Material document source path is invalid.");
        }

        var sourceIndex = 0;
        var sourceRelativePath = relativePath.Replace('\\', '/');
        if (sourceRoots.Count > 1)
        {
            var separatorIndex = sourceRelativePath.IndexOf('/');
            if (separatorIndex <= 0 ||
                !int.TryParse(sourceRelativePath[..separatorIndex], out sourceIndex) ||
                sourceIndex < 0 ||
                sourceIndex >= sourceRoots.Count)
            {
                throw new InvalidOperationException("Material document source path is invalid.");
            }

            sourceRelativePath = sourceRelativePath[(separatorIndex + 1)..];
        }

        var root = Path.GetFullPath(sourceRoots[sourceIndex]);
        var rootPrefix = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(root, sourceRelativePath));
        if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Material document source path escapes the configured root.");
        }

        var allowedExtensions = (_options.AllowedExtensions ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(NormalizeExtension)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (allowedExtensions.Count > 0 &&
            !allowedExtensions.Contains(Path.GetExtension(fullPath)))
        {
            throw new InvalidOperationException("Material document source file type is not allowed.");
        }

        return fullPath;
    }

    private IReadOnlyList<string> GetConfiguredSourceRoots()
    {
        var sourceRoots = (_options.SourceRoots ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (sourceRoots.Count > 0)
        {
            return sourceRoots;
        }

        return string.IsNullOrWhiteSpace(_options.SourceRoot)
            ? []
            : [_options.SourceRoot.Trim()];
    }

    private static string GetContentType(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".jfif" or ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".txt" => "text/plain",
            ".csv" => "text/csv",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".odt" => "application/vnd.oasis.opendocument.text",
            ".ods" => "application/vnd.oasis.opendocument.spreadsheet",
            _ => "application/octet-stream"
        };
    }

    private static MaterialDocumentSourceScan Available(
        string sourceLabel,
        bool isTruncated,
        IReadOnlyList<MaterialDocumentSourceFile> files)
    {
        return new MaterialDocumentSourceScan(
            sourceLabel,
            true,
            true,
            isTruncated,
            null,
            files);
    }

    private static MaterialDocumentSourceScan Unavailable(
        string sourceLabel,
        bool isConfigured,
        string errorCode)
    {
        return new MaterialDocumentSourceScan(
            sourceLabel,
            isConfigured,
            false,
            false,
            errorCode,
            []);
    }
}
