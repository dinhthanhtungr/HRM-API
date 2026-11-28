namespace HRM.Application.Features.PLM.Materials.TdsExtraction;

public interface ITdsDocumentNormalizer
{
    Task<TdsNormalizedDocument> NormalizeAsync(TdsInputFile file, CancellationToken cancellationToken);
}

public interface ITdsExtractionClient
{
    Task<TdsDocumentData> ExtractAsync(TdsNormalizedDocument document, CancellationToken cancellationToken);
}

public interface ITdsExcelRenderer
{
    byte[] Render(IReadOnlyList<TdsFileResult> results);
}

public interface ITdsJobQueue
{
    TdsStartResult TryEnqueue(Guid companyId, Guid requestedBy, IReadOnlyList<TdsInputFile>? uploads,
        int sourceOffset = 0, int? sourceLimit = null, bool allSourceBatches = false, Guid? resolvedSourceCompany = null);
    Task<TdsWorkItem> DequeueAsync(CancellationToken cancellationToken);
    TdsJobDto? Get(Guid jobId, Guid companyId, Guid requestedBy);
    byte[]? GetExcel(Guid jobId, Guid companyId, Guid requestedBy);
    bool Remove(Guid jobId, Guid companyId, Guid requestedBy);
    void SetProgress(Guid jobId, int totalFiles, int processedFiles, int? sourceTotalFiles = null, int? sourceOffset = null);
    void Complete(Guid jobId, byte[] excel);
    void Fail(Guid jobId, string code);
}
