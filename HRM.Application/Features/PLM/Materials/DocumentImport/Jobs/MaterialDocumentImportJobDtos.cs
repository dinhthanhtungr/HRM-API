using System.Text.Json.Serialization;
using HRM.Domain.Enums.Attachment;

namespace HRM.Application.Features.PLM.Materials.DocumentImport.Jobs;

public sealed class MaterialDocumentImportJobDto
{
    public Guid JobId { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public int TotalFiles { get; init; }
    public int ProcessedFiles { get; init; }
    public int ImportedFiles { get; init; }
    public int DuplicateFiles { get; init; }
    public int RequiresReviewFiles { get; init; }
    public int FailedFiles { get; init; }
    public int ExceptionCount { get; init; }
    public bool ExceptionsTruncated { get; init; }
    public string? ErrorCode { get; init; }
}

public sealed class MaterialDocumentImportJobExceptionDto
{
    public string RelativePath { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AttachmentSlot Slot { get; init; }

    public string ErrorCode { get; init; } = string.Empty;
    public IReadOnlyList<string> DetectedMaterialCodes { get; init; } = [];
}

internal static class MaterialDocumentImportJobMapper
{
    public static MaterialDocumentImportJobDto ToDto(
        MaterialDocumentImportJobSnapshot job)
    {
        return new MaterialDocumentImportJobDto
        {
            JobId = job.JobId,
            Status = job.Status,
            CreatedAt = job.CreatedAt,
            StartedAt = job.StartedAt,
            CompletedAt = job.CompletedAt,
            TotalFiles = job.TotalFiles,
            ProcessedFiles = job.ProcessedFiles,
            ImportedFiles = job.ImportedFiles,
            DuplicateFiles = job.DuplicateFiles,
            RequiresReviewFiles = job.RequiresReviewFiles,
            FailedFiles = job.FailedFiles,
            ExceptionCount = job.ExceptionCount,
            ExceptionsTruncated = job.ExceptionsTruncated,
            ErrorCode = job.ErrorCode
        };
    }

    public static MaterialDocumentImportJobExceptionDto ToDto(
        MaterialDocumentImportJobException exception)
    {
        return new MaterialDocumentImportJobExceptionDto
        {
            RelativePath = exception.RelativePath,
            FileName = exception.FileName,
            Slot = exception.Slot,
            ErrorCode = exception.ErrorCode,
            DetectedMaterialCodes = exception.DetectedMaterialCodes
        };
    }
}
