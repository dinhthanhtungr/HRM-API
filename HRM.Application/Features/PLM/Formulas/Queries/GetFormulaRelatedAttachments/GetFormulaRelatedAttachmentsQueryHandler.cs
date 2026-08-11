using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.Attachments.Services;
using HRM.Application.Features.PLM.Formulas.Dtos.RelatedAttachments;
using HRM.Domain.Enums.Formulas;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Formulas.Queries.GetFormulaRelatedAttachments;

/// <summary>
/// Returns lightweight attachment metadata for active material rows in one formula.
/// File bytes are still served by the material attachment endpoint so parent ownership is checked again.
/// </summary>
internal sealed class GetFormulaRelatedAttachmentsQueryHandler
    : IRequestHandler<GetFormulaRelatedAttachmentsQuery, FormulaRelatedAttachmentsDto?>
{
    private readonly IPLMReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public GetFormulaRelatedAttachmentsQueryHandler(
        IPLMReadDbContext dbContext,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<FormulaRelatedAttachmentsDto?> Handle(
        GetFormulaRelatedAttachmentsQuery request,
        CancellationToken cancellationToken)
    {
        if (request.FormulaId == Guid.Empty ||
            _currentUser.CompanyId is not { } companyId ||
            companyId == Guid.Empty)
        {
            return null;
        }

        var formulaExists = await _dbContext.Formulas
            .AsNoTracking()
            .AnyAsync(x =>
                x.FormulaId == request.FormulaId &&
                x.IsActive &&
                x.CompanyId == companyId &&
                x.Product.CompanyId == companyId &&
                x.Product.IsActive,
                cancellationToken);

        if (!formulaExists)
        {
            return null;
        }

        var materialRows = await _dbContext.FormulaMaterials
            .AsNoTracking()
            .Where(x =>
                x.FormulaId == request.FormulaId &&
                x.IsActive &&
                x.itemType == ItemType.Material &&
                x.MaterialId.HasValue &&
                x.Material != null &&
                x.Material.CompanyId == companyId &&
                x.Material.IsActive == true &&
                x.Material.AttachmentCollectionId.HasValue)
            .Select(x => new MaterialSourceProjection
            {
                MaterialId = x.MaterialId!.Value,
                ExternalId = x.Material.ExternalId,
                Name = x.Material.Name,
                AttachmentCollectionId = x.Material.AttachmentCollectionId!.Value
            })
            .ToListAsync(cancellationToken);

        var materials = materialRows
            .GroupBy(x => x.MaterialId)
            .Select(x => x.First())
            .ToList();

        if (materials.Count == 0)
        {
            return new FormulaRelatedAttachmentsDto
            {
                FormulaId = request.FormulaId,
                TotalCount = 0,
                Groups = []
            };
        }

        var collectionIds = materials
            .Select(x => x.AttachmentCollectionId)
            .Distinct()
            .ToList();

        var attachments = await _dbContext.AttachmentModels
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                collectionIds.Contains(x.AttachmentCollectionId))
            .OrderBy(x => x.CreateDate)
            .Select(x => new AttachmentProjection
            {
                AttachmentCollectionId = x.AttachmentCollectionId,
                AttachmentId = x.AttachmentId,
                FileName = x.FileName,
                SizeBytes = x.SizeBytes,
                CreatedDate = x.CreateDate
            })
            .ToListAsync(cancellationToken);

        var attachmentsByCollection = attachments
            .GroupBy(x => x.AttachmentCollectionId)
            .ToDictionary(
                x => x.Key,
                x => x.GroupBy(attachment => attachment.AttachmentId)
                    .Select(attachment => attachment.First())
                    .ToList());

        var groups = materials
            .Select(material => new FormulaRelatedAttachmentGroupDto
            {
                SourceId = material.MaterialId,
                SourceExternalId = material.ExternalId,
                SourceName = material.Name,
                Attachments = attachmentsByCollection.TryGetValue(
                    material.AttachmentCollectionId,
                    out var materialAttachments)
                    ? materialAttachments
                        .Select(x => ToDto(material.MaterialId, x))
                        .ToList()
                    : []
            })
            .Where(x => x.Attachments.Count > 0)
            .ToList();

        return new FormulaRelatedAttachmentsDto
        {
            FormulaId = request.FormulaId,
            TotalCount = groups
                .SelectMany(x => x.Attachments)
                .Select(x => x.AttachmentId)
                .Distinct()
                .Count(),
            Groups = groups
        };
    }

    private static FormulaRelatedAttachmentDto ToDto(
        Guid materialId,
        AttachmentProjection attachment)
    {
        var contentUrl = BuildContentUrl(materialId, attachment.AttachmentId);
        var extension = Path.GetExtension(attachment.FileName);

        return new FormulaRelatedAttachmentDto
        {
            AttachmentId = attachment.AttachmentId,
            FileName = attachment.FileName,
            SizeBytes = attachment.SizeBytes,
            ContentType = ResolveContentType(extension),
            IsImage = AttachmentFileHelper.IsImageFile(attachment.FileName),
            IsPdf = extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase),
            ContentUrl = contentUrl,
            DownloadUrl = $"{contentUrl}?mode=download",
            CreatedDate = attachment.CreatedDate
        };
    }

    private static string BuildContentUrl(Guid materialId, Guid attachmentId)
    {
        return $"/api/v1/plm/materials/{materialId}/attachments/{attachmentId}/content";
    }

    private static string ResolveContentType(string? extension)
    {
        return extension?.ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            _ => "application/octet-stream"
        };
    }

    private sealed class MaterialSourceProjection
    {
        public Guid MaterialId { get; init; }
        public string? ExternalId { get; init; }
        public string? Name { get; init; }
        public Guid AttachmentCollectionId { get; init; }
    }

    private sealed class AttachmentProjection
    {
        public Guid AttachmentCollectionId { get; init; }
        public Guid AttachmentId { get; init; }
        public string FileName { get; init; } = string.Empty;
        public long SizeBytes { get; init; }
        public DateTime CreatedDate { get; init; }
    }
}
