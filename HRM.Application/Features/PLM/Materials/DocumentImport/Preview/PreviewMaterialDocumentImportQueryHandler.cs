using HRM.Application.Abstractions.FileStorage;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.PLM.Materials.DocumentImport.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Materials.DocumentImport.Preview;

internal sealed class PreviewMaterialDocumentImportQueryHandler
    : IRequestHandler<PreviewMaterialDocumentImportQuery, MaterialDocumentImportPreviewDto?>
{
    private const string ExactMatch = "exact_match";
    private const string Ambiguous = "ambiguous";
    private const string Unmatched = "unmatched";

    private readonly IPLMReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IMaterialDocumentSourceScanner _sourceScanner;

    public PreviewMaterialDocumentImportQueryHandler(
        IPLMReadDbContext dbContext,
        ICurrentUser currentUser,
        IMaterialDocumentSourceScanner sourceScanner)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _sourceScanner = sourceScanner;
    }

    public async Task<MaterialDocumentImportPreviewDto?> Handle(
        PreviewMaterialDocumentImportQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty)
        {
            return null;
        }

        var source = await _sourceScanner.ScanAsync(cancellationToken);
        if (!source.IsAvailable)
        {
            return BuildUnavailableResult(source);
        }

        var materials = await _dbContext.Materials
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == companyId &&
                x.IsActive == true &&
                x.ExternalId != null &&
                x.ExternalId != string.Empty)
            .Select(x => new MaterialMatchCandidate(
                x.MaterialId,
                x.ExternalId!,
                x.Name))
            .ToListAsync(cancellationToken);

        var materialsByCode = materials
            .GroupBy(
                x => MaterialDocumentFileNameParser.NormalizeMaterialCode(x.ExternalId),
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.ToArray(),
                StringComparer.OrdinalIgnoreCase);

        var items = source.Files
            .Select(file => BuildItem(file, materialsByCode))
            .OrderBy(item => item.MatchStatus == ExactMatch ? 0 : 1)
            .ThenBy(item => item.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new MaterialDocumentImportPreviewDto
        {
            SourceLabel = source.SourceLabel,
            SourceStatus = "available",
            IsTruncated = source.IsTruncated,
            Summary = new MaterialDocumentImportPreviewSummaryDto
            {
                TotalFiles = items.Length,
                ExactMatches = items.Count(x => x.MatchStatus == ExactMatch),
                AmbiguousMatches = items.Count(x => x.MatchStatus == Ambiguous),
                UnmatchedFiles = items.Count(x => x.MatchStatus == Unmatched),
                RequiresReview = items.Count(x => x.RequiresReview)
            },
            Items = items
        };
    }

    private static MaterialDocumentImportPreviewItemDto BuildItem(
        MaterialDocumentSourceFile file,
        IReadOnlyDictionary<string, MaterialMatchCandidate[]> materialsByCode)
    {
        var parsed = MaterialDocumentFileNameParser.Parse(file.FileName);
        var matchingMaterials = parsed.MaterialCodes
            .Where(materialsByCode.ContainsKey)
            .SelectMany(code => materialsByCode[code])
            .DistinctBy(x => x.MaterialId)
            .ToArray();

        var matchStatus = matchingMaterials.Length switch
        {
            1 when parsed.MaterialCodes.Count == 1 => ExactMatch,
            > 0 => Ambiguous,
            _ => Unmatched
        };
        var notes = parsed.Notes.ToList();

        if (parsed.MaterialCodes.Count == 0)
        {
            notes.Add("material_code_not_detected");
        }
        else if (matchingMaterials.Length == 0)
        {
            notes.Add("material_not_found_in_current_company");
        }
        else if (matchStatus == Ambiguous)
        {
            notes.Add("material_match_ambiguous");
        }

        var material = matchStatus == ExactMatch ? matchingMaterials[0] : null;
        var requiresReview = matchStatus != ExactMatch ||
                             notes.Contains("tds_typo_detected", StringComparer.Ordinal) ||
                             notes.Contains("material_document_slot_ambiguous", StringComparer.Ordinal);

        return new MaterialDocumentImportPreviewItemDto
        {
            RelativePath = file.RelativePath,
            FileName = file.FileName,
            SizeBytes = file.SizeBytes,
            LastWriteTime = file.LastWriteTime,
            Slot = parsed.Slot,
            MatchStatus = matchStatus,
            RequiresReview = requiresReview,
            DetectedMaterialCodes = parsed.MaterialCodes,
            MaterialId = material?.MaterialId,
            MaterialExternalId = material?.ExternalId,
            MaterialName = material?.Name,
            Notes = notes
        };
    }

    private static MaterialDocumentImportPreviewDto BuildUnavailableResult(
        MaterialDocumentSourceScan source)
    {
        return new MaterialDocumentImportPreviewDto
        {
            SourceLabel = source.SourceLabel,
            SourceStatus = source.IsConfigured ? "unavailable" : "not_configured",
            ErrorCode = source.ErrorCode,
            IsTruncated = false
        };
    }

    private sealed record MaterialMatchCandidate(
        Guid MaterialId,
        string ExternalId,
        string? Name);
}
