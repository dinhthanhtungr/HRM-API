using HRM.Domain.Enums.Attachment;

namespace HRM.Application.Features.PLM.Materials.DocumentImport.Jobs;

public interface IMaterialDocumentImportJobQueue
{
    bool TryEnqueue(
        Guid companyId,
        Guid requestedByUserId,
        Guid? requestedByEmployeeId,
        out MaterialDocumentImportJobSnapshot job);

    ValueTask<MaterialDocumentImportJobWorkItem> DequeueAsync(
        CancellationToken cancellationToken);

    MaterialDocumentImportJobSnapshot? Get(Guid jobId, Guid companyId);

    IReadOnlyList<MaterialDocumentImportJobException> GetExceptions(
        Guid jobId,
        Guid companyId);

    void MarkRunning(Guid jobId, int totalFiles);
    void RecordItem(
        Guid jobId,
        MaterialDocumentImportItemOutcome outcome,
        MaterialDocumentImportJobException? exception = null);
    void MarkCompleted(Guid jobId);
    void MarkFailed(Guid jobId, string errorCode);
}

public sealed record MaterialDocumentImportJobWorkItem(
    Guid JobId,
    Guid CompanyId,
    Guid RequestedByUserId,
    Guid? RequestedByEmployeeId);

public sealed record MaterialDocumentImportJobSnapshot(
    Guid JobId,
    Guid CompanyId,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    int TotalFiles,
    int ProcessedFiles,
    int ImportedFiles,
    int DuplicateFiles,
    int RequiresReviewFiles,
    int FailedFiles,
    int ExceptionCount,
    bool ExceptionsTruncated,
    string? ErrorCode);

public sealed record MaterialDocumentImportJobException(
    string RelativePath,
    string FileName,
    AttachmentSlot Slot,
    string ErrorCode,
    IReadOnlyList<string> DetectedMaterialCodes);

public enum MaterialDocumentImportItemOutcome
{
    Imported,
    Duplicate,
    RequiresReview,
    Failed
}

public static class MaterialDocumentImportJobStatuses
{
    public const string Queued = "queued";
    public const string Running = "running";
    public const string Completed = "completed";
    public const string Failed = "failed";
}
