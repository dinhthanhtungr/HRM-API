namespace HRM.Application.Features.PLM.Materials.TdsExtraction;

public sealed class TdsExtractionOptions
{
    public const string SectionName = "MaterialTdsExtraction";
    public bool Enabled { get; set; }
    public int MaxFiles { get; set; } = 30;
    public int MaxFileBytes { get; set; } = 10 * 1024 * 1024;
    public int MaxBatchBytes { get; set; } = 40 * 1024 * 1024;
    public int RetentionMinutes { get; set; } = 60;
    public int MaxRetainedJobs { get; set; } = 8;
    public int MaxTextCharacters { get; set; } = 200_000;
    public string? LibreOfficePath { get; set; }
    public Guid? SourceCompanyId { get; set; }
}

public sealed record TdsInputFile(string FileName, byte[] Content);
public sealed record TdsWorkItem(Guid JobId, Guid CompanyId, Guid RequestedBy, IReadOnlyList<TdsInputFile>? Uploads,
    int SourceOffset = 0, int? SourceLimit = null);
public sealed record TdsJobDto(Guid JobId, string Status, int TotalFiles, int ProcessedFiles,
    string? ErrorCode, DateTimeOffset CreatedAt, DateTimeOffset? CompletedAt,
    int? SourceOffset = null, int? SourceTotalFiles = null, int? NextSourceOffset = null);
public sealed record TdsStartResult(bool Accepted, TdsJobDto? Job, string? ErrorCode);
public sealed record TdsNormalizedDocument(string MimeType, byte[]? Binary, string? Text, string Method);
public sealed record TdsMaterialCandidate(Guid MaterialId, string ExternalId, string? Name);

public sealed class TdsDocumentData
{
    public string? DocumentType { get; set; }
    public string? ProductName { get; set; }
    public string? ProductCode { get; set; }
    public string? Manufacturer { get; set; }
    public string? Revision { get; set; }
    public string? IssueDate { get; set; }
    public string? Language { get; set; }
    public string? Applications { get; set; }
    public string? Storage { get; set; }
    public List<string> MaterialCodes { get; set; } = [];
    public List<TdsSpecification> Specifications { get; set; } = [];
}

public sealed class TdsSpecification
{
    [System.Text.Json.Serialization.JsonIgnore]
    public List<string> ValidationCodes { get; set; } = [];
    public string? Name { get; set; }
    public string? Value { get; set; }
    public string? Unit { get; set; }
    public string? TestMethod { get; set; }
    public string? Conditions { get; set; }
    public string? SourceLocation { get; set; }
    public string? Evidence { get; set; }
}

public sealed class TdsFileResult
{
    public required string FileName { get; init; }
    public string? Hash { get; set; }
    public string Status { get; set; } = "failed";
    public string? DuplicateOf { get; set; }
    public string? NormalizationMethod { get; set; }
    public TdsDocumentData? Data { get; set; }
    public string MatchStatus { get; set; } = "unmatched";
    public Guid? MaterialId { get; set; }
    public string? MaterialExternalId { get; set; }
    public List<TdsMaterialCandidate> Candidates { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
}

/// <summary>Lỗi có mã ổn định; không đưa nội dung tài liệu hoặc response nhà cung cấp ra API/log.</summary>
public sealed class TdsProcessingException(string code) : Exception(code)
{
    public string Code { get; } = code;
}
