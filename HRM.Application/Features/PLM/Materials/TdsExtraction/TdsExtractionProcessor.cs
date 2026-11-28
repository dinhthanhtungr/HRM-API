using System.Security.Cryptography;
using HRM.Application.Abstractions.FileStorage;
using HRM.Application.Abstractions.Persistence.PLM;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HRM.Application.Features.PLM.Materials.TdsExtraction;

/// <summary>Chỉ đọc dữ liệu NVL và file nguồn; kết quả là Excel, không gọi SaveChanges hay import attachment.</summary>
public sealed class TdsExtractionProcessor(IPLMReadDbContext db, IMaterialDocumentSourceScanner scanner,
    ITdsDocumentNormalizer normalizer, ITdsExtractionClient client, ITdsExcelRenderer renderer,
    ITdsJobQueue queue, IOptions<TdsExtractionOptions> options)
{
    public async Task ProcessAsync(TdsWorkItem job, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        IReadOnlyList<MaterialDocumentSourceFile>? sourceFiles = null;
        int? sourceTotalFiles = null;
        if (job.Uploads is null)
        {
            if (!settings.UsesSharedSource(job.AllSourceBatches) &&
                await TdsSourceCompany.ResolveAsync(db, settings.SourceCompanyId, cancellationToken) != job.CompanyId)
                throw new TdsProcessingException("source_not_authorized");
            var scan = await scanner.ScanAsync(cancellationToken);
            if (!scan.IsAvailable) throw new TdsProcessingException(scan.ErrorCode ?? "source_unavailable");
            if (scan.IsTruncated) throw new TdsProcessingException("source_scan_truncated");
            sourceTotalFiles = scan.Files.Count;
            var ordered = scan.Files.OrderBy(x => x.RelativePath, StringComparer.OrdinalIgnoreCase);
            sourceFiles = job.AllSourceBatches ? ordered.ToArray()
                : ordered.Skip(job.SourceOffset).Take(job.SourceLimit ?? settings.MaxFiles).ToArray();
            if (sourceFiles.Count == 0) throw new TdsProcessingException("source_batch_empty");
            if (!job.AllSourceBatches && sourceFiles.Sum(x => x.SizeBytes) > settings.MaxBatchBytes)
                throw new TdsProcessingException("batch_size_limit");
        }
        var total = job.Uploads?.Count ?? sourceFiles!.Count;
        queue.SetProgress(job.JobId, total, 0, sourceTotalFiles, sourceFiles is null ? null : job.SourceOffset);
        var materials = await db.Materials.AsNoTracking()
            .Where(x => x.CompanyId == job.CompanyId && x.IsActive == true && x.ExternalId != null && x.ExternalId != "")
            .Select(x => new TdsMaterialCandidate(x.MaterialId, x.ExternalId!, x.Name))
            .ToListAsync(cancellationToken);
        var results = new List<TdsFileResult>();
        var seen = new Dictionary<string, string>(StringComparer.Ordinal);
        long batchBytes = 0;
        var batchFiles = 0;
        for (var index = 0; index < total; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var name = job.Uploads?[index].FileName ?? sourceFiles![index].FileName;
            var result = new TdsFileResult { FileName = name };
            results.Add(result);
            try
            {
                TdsInputFile input;
                if (job.Uploads is { } uploads) input = uploads[index];
                else
                {
                    var file = sourceFiles![index];
                    TdsExtractionRules.ValidateFile(name, file.SizeBytes, settings);
                    var source = await scanner.OpenReadAsync(file.RelativePath, cancellationToken);
                    await using var stream = source.Stream;
                    input = new(name, await TdsFileReader.ReadBoundedAsync(stream, settings.MaxFileBytes, cancellationToken));
                }
                TdsExtractionRules.ValidateFile(name, input.Content.LongLength, settings);
                if (job.AllSourceBatches && (batchFiles >= settings.MaxFiles ||
                    batchBytes + input.Content.LongLength > settings.MaxBatchBytes))
                {
                    batchBytes = 0;
                    batchFiles = 0;
                }
                batchFiles++;
                batchBytes += input.Content.LongLength;
                if (batchBytes > settings.MaxBatchBytes) throw new TdsProcessingException("batch_size_limit");
                result.Hash = Convert.ToHexString(SHA256.HashData(input.Content));
                if (seen.TryGetValue(result.Hash, out var duplicate))
                {
                    result.Status = "duplicate";
                    result.DuplicateOf = duplicate;
                }
                else
                {
                    var document = await normalizer.NormalizeAsync(input, cancellationToken);
                    result.NormalizationMethod = document.Method;
                    result.Data = await client.ExtractAsync(document, cancellationToken);
                    TdsExtractionRules.ValidateAndMatch(result, materials, document);
                    result.Status = "extracted";
                    if (job.AllSourceBatches && !TdsExtractionRules.IsTechnicalOrSafetySheet(result.Data.DocumentType))
                    {
                        result.Status = "excluded";
                        result.Warnings.Add("outside_tds_sds_scope");
                        result.Data.Specifications.Clear();
                    }
                    if (result.Data.Specifications.Any(x => x.Name?.Length > 32767 || x.Value?.Length > 32767))
                        throw new TdsProcessingException("excel_original_cell_limit");
                    // Failed files do not poison deduplication: another copy can be retried.
                    seen[result.Hash] = name;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                result.Status = "failed";
                result.Data = null;
                result.Warnings.Add(exception is TdsProcessingException known ? known.Code : "file_processing_failed");
            }
            queue.SetProgress(job.JobId, total, index + 1);
        }
        queue.Complete(job.JobId, renderer.Render(results));
    }
}
