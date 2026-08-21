using System.Security.Cryptography;
using HRM.Application.Abstractions.FileStorage;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.Attachments.Dtos;
using HRM.Application.Features.Attachments.Services;
using HRM.Domain.Entities.AttachmentSchema;
using HRM.Domain.Entities.MaterialSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Materials.DocumentImport.Jobs;

public sealed class MaterialDocumentImportProcessor
    : IMaterialDocumentImportProcessor
{
    private readonly IPLMWriteDbContext _dbContext;
    private readonly IMaterialDocumentSourceScanner _sourceScanner;
    private readonly IAttachmentService _attachmentService;
    private readonly IMaterialDocumentImportJobQueue _jobQueue;
    private readonly Dictionary<Guid, HashSet<string>> _hashesByCollectionId = [];
    private readonly Dictionary<Guid, Material> _trackedMaterialsById = [];

    public MaterialDocumentImportProcessor(
        IPLMWriteDbContext dbContext,
        IMaterialDocumentSourceScanner sourceScanner,
        IAttachmentService attachmentService,
        IMaterialDocumentImportJobQueue jobQueue)
    {
        _dbContext = dbContext;
        _sourceScanner = sourceScanner;
        _attachmentService = attachmentService;
        _jobQueue = jobQueue;
    }

    public async Task ProcessAsync(
        MaterialDocumentImportJobWorkItem job,
        CancellationToken cancellationToken)
    {
        _jobQueue.MarkRunning(job.JobId, 0);
        var source = await _sourceScanner.ScanAsync(cancellationToken);
        if (!source.IsAvailable)
        {
            _jobQueue.MarkFailed(
                job.JobId,
                source.ErrorCode ?? "source_unavailable");
            return;
        }

        if (source.IsTruncated)
        {
            _jobQueue.MarkFailed(job.JobId, "source_scan_truncated");
            return;
        }

        _jobQueue.MarkRunning(job.JobId, source.Files.Count);
        var materials = await _dbContext.Materials
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == job.CompanyId &&
                x.IsActive == true &&
                x.ExternalId != null &&
                x.ExternalId != string.Empty)
            .Select(x => new MaterialMatchCandidate(
                x.MaterialId,
                x.ExternalId!,
                x.CompanyId))
            .ToListAsync(cancellationToken);
        var materialsByCode = materials
            .GroupBy(
                x => MaterialDocumentFileNameParser.NormalizeMaterialCode(x.ExternalId!),
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.ToArray(),
                StringComparer.OrdinalIgnoreCase);

        foreach (var file in source.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var parsed = MaterialDocumentFileNameParser.Parse(file.FileName);
            var matchingMaterials = parsed.MaterialCodes
                .Where(materialsByCode.ContainsKey)
                .SelectMany(code => materialsByCode[code])
                .DistinctBy(x => x.MaterialId)
                .ToArray();

            if (parsed.MaterialCodes.Count != 1 || matchingMaterials.Length != 1)
            {
                _jobQueue.RecordItem(
                    job.JobId,
                    MaterialDocumentImportItemOutcome.RequiresReview,
                    BuildReviewException(file, parsed, matchingMaterials.Length));
                continue;
            }

            try
            {
                var outcome = await ImportFileAsync(
                    file,
                    parsed,
                    matchingMaterials[0],
                    job.RequestedByEmployeeId,
                    cancellationToken);
                _jobQueue.RecordItem(job.JobId, outcome);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _jobQueue.RecordItem(
                    job.JobId,
                    MaterialDocumentImportItemOutcome.Failed,
                    new MaterialDocumentImportJobException(
                        file.RelativePath,
                        file.FileName,
                        parsed.Slot,
                        ResolveImportErrorCode(exception),
                        parsed.MaterialCodes));
            }
        }

        _jobQueue.MarkCompleted(job.JobId);
    }

    private async Task<MaterialDocumentImportItemOutcome> ImportFileAsync(
        MaterialDocumentSourceFile file,
        MaterialDocumentFileNameParseResult parsed,
        MaterialMatchCandidate materialCandidate,
        Guid? requestedByEmployeeId,
        CancellationToken cancellationToken)
    {
        var source = await _sourceScanner.OpenReadAsync(
            file.RelativePath,
            cancellationToken);
        await using var sourceStream = source.Stream;
        if (!sourceStream.CanSeek)
        {
            throw new InvalidOperationException("Source stream must be seekable.");
        }

        var hashBytes = await SHA256.HashDataAsync(sourceStream, cancellationToken);
        var contentHash = Convert.ToHexString(hashBytes);
        sourceStream.Position = 0;

        var material = await GetTrackedMaterialAsync(
            materialCandidate,
            cancellationToken);
        var collectionId = await EnsureAttachmentCollectionAsync(
            material,
            requestedByEmployeeId,
            cancellationToken);
        var knownHashes = await GetKnownHashesAsync(collectionId, cancellationToken);
        if (knownHashes.Contains(contentHash))
        {
            return MaterialDocumentImportItemOutcome.Duplicate;
        }

        await _attachmentService.UploadListAsync(
            collectionId,
            parsed.Slot,
            [
                new AttachmentUploadFile(
                    sourceStream,
                    source.FileName,
                    source.ContentType,
                    source.Length,
                    contentHash)
            ],
            requestedByEmployeeId,
            cancellationToken);

        knownHashes.Add(contentHash);
        return MaterialDocumentImportItemOutcome.Imported;
    }

    private async Task<Material> GetTrackedMaterialAsync(
        MaterialMatchCandidate materialCandidate,
        CancellationToken cancellationToken)
    {
        if (_trackedMaterialsById.TryGetValue(materialCandidate.MaterialId, out var cached))
        {
            return cached;
        }

        var material = await _dbContext.Materials
            .FirstOrDefaultAsync(
                x =>
                    x.MaterialId == materialCandidate.MaterialId &&
                    x.CompanyId == materialCandidate.CompanyId &&
                    x.IsActive == true,
                cancellationToken)
            ?? throw new InvalidOperationException("Material is no longer active.");
        _trackedMaterialsById[materialCandidate.MaterialId] = material;
        return material;
    }

    private async Task<Guid> EnsureAttachmentCollectionAsync(
        Material material,
        Guid? requestedByEmployeeId,
        CancellationToken cancellationToken)
    {
        if (material.AttachmentCollectionId is { } existingId && existingId != Guid.Empty)
        {
            var collectionExists = await _dbContext.AttachmentCollections
                .AnyAsync(x => x.AttachmentCollectionId == existingId, cancellationToken);
            if (!collectionExists)
            {
                await _dbContext.AttachmentCollections.AddAsync(
                    new AttachmentCollection { AttachmentCollectionId = existingId },
                    cancellationToken);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            return existingId;
        }

        var collectionId = Guid.CreateVersion7();
        await _dbContext.AttachmentCollections.AddAsync(
            new AttachmentCollection { AttachmentCollectionId = collectionId },
            cancellationToken);
        material.AttachmentCollectionId = collectionId;
        material.UpdatedDate = DateTime.Now;
        material.UpdatedBy = requestedByEmployeeId;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return collectionId;
    }

    private async Task<HashSet<string>> GetKnownHashesAsync(
        Guid collectionId,
        CancellationToken cancellationToken)
    {
        if (_hashesByCollectionId.TryGetValue(collectionId, out var cached))
        {
            return cached;
        }

        var hashes = await _dbContext.AttachmentModels
            .AsNoTracking()
            .Where(x =>
                x.AttachmentCollectionId == collectionId &&
                x.IsActive &&
                x.ContentHash != null &&
                x.ContentHash != string.Empty)
            .Select(x => x.ContentHash!)
            .ToListAsync(cancellationToken);
        var result = hashes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        _hashesByCollectionId[collectionId] = result;
        return result;
    }

    private static MaterialDocumentImportJobException BuildReviewException(
        MaterialDocumentSourceFile file,
        MaterialDocumentFileNameParseResult parsed,
        int matchingMaterialCount)
    {
        var errorCode = parsed.MaterialCodes.Count switch
        {
            0 => "material_code_not_detected",
            > 1 => "multiple_material_codes_detected",
            _ when matchingMaterialCount == 0 => "material_not_found_in_current_company",
            _ => "material_match_ambiguous"
        };

        return new MaterialDocumentImportJobException(
            file.RelativePath,
            file.FileName,
            parsed.Slot,
            errorCode,
            parsed.MaterialCodes);
    }

    private static string ResolveImportErrorCode(Exception exception)
    {
        return exception switch
        {
            FileNotFoundException => "source_file_not_found",
            UnauthorizedAccessException => "source_access_denied",
            IOException => "source_io_error",
            InvalidOperationException => "file_validation_failed",
            _ => "import_failed"
        };
    }

    private sealed record MaterialMatchCandidate(
        Guid MaterialId,
        string ExternalId,
        Guid CompanyId);
}
