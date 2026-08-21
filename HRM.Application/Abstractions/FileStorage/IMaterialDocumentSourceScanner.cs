namespace HRM.Application.Abstractions.FileStorage;

/// <summary>
/// Đọc metadata tệp từ thư mục nguồn TDS/MSDS đã được cấu hình ở backend.
/// Không mở nội dung, di chuyển hoặc thay đổi tệp nguồn.
/// </summary>
public interface IMaterialDocumentSourceScanner
{
    Task<MaterialDocumentSourceScan> ScanAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Mở tệp theo đường dẫn tương đối đã trả về từ ScanAsync. Implementation
    /// phải chặn absolute path và path traversal ra ngoài thư mục nguồn.
    /// </summary>
    Task<MaterialDocumentSourceContent> OpenReadAsync(
        string relativePath,
        CancellationToken cancellationToken = default);
}

public sealed record MaterialDocumentSourceFile(
    string RelativePath,
    string FileName,
    long SizeBytes,
    DateTime LastWriteTime);

public sealed record MaterialDocumentSourceScan(
    string SourceLabel,
    bool IsConfigured,
    bool IsAvailable,
    bool IsTruncated,
    string? ErrorCode,
    IReadOnlyList<MaterialDocumentSourceFile> Files);

public sealed record MaterialDocumentSourceContent(
    Stream Stream,
    string FileName,
    string ContentType,
    long Length);
