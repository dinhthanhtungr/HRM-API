using HRM.Application.Abstractions.FileStorage;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.Attachments.Dtos;
using HRM.Domain.Entities.AttachmentSchema;
using HRM.Domain.Enums.Attachment;
using HRM.Domain.Security.Rules.Attachment;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Attachments.Services;

internal sealed class AttachmentService : IAttachmentService
{
    private readonly IPLMWriteDbContext _dbContext;
    private readonly IFileStorage _fileStorage;

    public AttachmentService(IPLMWriteDbContext dbContext, IFileStorage fileStorage)
    {
        _dbContext = dbContext;
        _fileStorage = fileStorage;
    }

    public async Task<IReadOnlyList<AttachmentDto>> UploadListAsync(
        Guid collectionId,
        AttachmentSlot slot,
        IReadOnlyList<AttachmentUploadFile> files,
        Guid? createdBy,
        CancellationToken cancellationToken = default)
    {
        if (files.Count == 0)
        {
            return Array.Empty<AttachmentDto>();
        }

        var collectionExists = await _dbContext.AttachmentCollections
            .AnyAsync(x => x.AttachmentCollectionId == collectionId, cancellationToken);

        if (!collectionExists)
        {
            throw new InvalidOperationException("Attachment collection does not exist.");
        }

        var rule = GetRule(slot);
        await ValidateUploadAsync(collectionId, slot, files, rule, cancellationToken);

        var attachments = new List<AttachmentModel>(files.Count);
        var savedPaths = new List<string>(files.Count);
        var relativeFolder = BuildRelativeFolder(collectionId, slot);

        try
        {
            foreach (var file in files)
            {
                var storagePath = await _fileStorage.SaveAsync(
                    file.Stream,
                    file.ContentType,
                    file.FileName,
                    relativeFolder,
                    cancellationToken);
                savedPaths.Add(storagePath);

                attachments.Add(new AttachmentModel
                {
                    AttachmentId = Guid.CreateVersion7(),
                    AttachmentCollectionId = collectionId,
                    Slot = slot,
                    FileName = Path.GetFileName(file.FileName),
                    SizeBytes = file.Length,
                    StoragePath = storagePath,
                    CreateDate = DateTime.Now,
                    CreateBy = createdBy,
                    IsActive = true,
                    ContentHash = NormalizeContentHash(file.ContentHash)
                });
            }

            await _dbContext.AttachmentModels.AddRangeAsync(attachments, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            foreach (var storagePath in savedPaths)
            {
                try
                {
                    await _fileStorage.DeleteAsync(storagePath, CancellationToken.None);
                }
                catch
                {
                    // Không che exception gốc của thao tác upload.
                }
            }

            throw;
        }

        return attachments.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<AttachmentDto>> ListAsync(
        Guid collectionId,
        AttachmentSlot? slot,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.AttachmentModels
            .AsNoTracking()
            .Where(x => x.AttachmentCollectionId == collectionId && x.IsActive);

        if (slot.HasValue)
        {
            query = query.Where(x => x.Slot == slot.Value);
        }

        var attachments = await query
            .OrderBy(x => x.CreateDate)
            .ToListAsync(cancellationToken);

        return attachments.Select(ToDto).ToList();
    }

    public async Task<AttachmentContent> GetContentAsync(Guid attachmentId, CancellationToken cancellationToken = default)
    {
        var attachment = await _dbContext.AttachmentModels
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.AttachmentId == attachmentId && x.IsActive, cancellationToken);

        if (attachment is null)
        {
            throw new FileNotFoundException("Attachment was not found.");
        }

        var file = await _fileStorage.OpenReadAsync(attachment.StoragePath, cancellationToken);

        return new AttachmentContent(
            file.Stream,
            file.ContentType,
            attachment.FileName,
            file.Length);
    }

    public async Task DeleteAsync(Guid attachmentId, CancellationToken cancellationToken = default)
    {
        var attachment = await _dbContext.AttachmentModels
            .FirstOrDefaultAsync(x => x.AttachmentId == attachmentId && x.IsActive, cancellationToken);

        if (attachment is null)
        {
            return;
        }

        attachment.IsActive = false;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task HardDeleteAsync(Guid attachmentId, CancellationToken cancellationToken = default)
    {
        var attachment = await _dbContext.AttachmentModels
            .FirstOrDefaultAsync(x => x.AttachmentId == attachmentId, cancellationToken);

        if (attachment is null)
        {
            return;
        }

        _dbContext.AttachmentModels.Remove(attachment);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _fileStorage.DeleteAsync(attachment.StoragePath, cancellationToken);
    }

    private async Task ValidateUploadAsync(
        Guid collectionId,
        AttachmentSlot slot,
        IReadOnlyList<AttachmentUploadFile> files,
        SlotRule rule,
        CancellationToken cancellationToken)
    {
        if (!rule.AllowMultiple)
        {
            var activeCount = await _dbContext.AttachmentModels
                .CountAsync(x => x.AttachmentCollectionId == collectionId && x.Slot == slot && x.IsActive, cancellationToken);

            if (activeCount + files.Count > 1)
            {
                throw new InvalidOperationException($"Slot {slot} allows only one active attachment.");
            }
        }

        foreach (var file in files)
        {
            if (file.Length <= 0)
            {
                throw new InvalidOperationException($"File {file.FileName} is empty.");
            }

            if (file.Length > rule.MaxBytes)
            {
                throw new InvalidOperationException($"File {file.FileName} exceeds the maximum allowed size.");
            }

            if (!IsAllowedContentType(file.ContentType, rule.AllowedMimePrefixes))
            {
                throw new InvalidOperationException($"File {file.FileName} has an unsupported content type.");
            }

            _ = NormalizeContentHash(file.ContentHash);
        }
    }

    private static string? NormalizeContentHash(string? contentHash)
    {
        if (string.IsNullOrWhiteSpace(contentHash))
        {
            return null;
        }

        var normalized = contentHash.Trim().ToUpperInvariant();
        if (normalized.Length != 64 || normalized.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new InvalidOperationException("Attachment content hash must be a SHA-256 hex value.");
        }

        return normalized;
    }

    private static SlotRule GetRule(AttachmentSlot slot)
    {
        return AttachmentRules.Map.TryGetValue(slot, out var rule)
            ? rule
            : throw new InvalidOperationException($"Attachment slot {slot} is not configured.");
    }

    private static bool IsAllowedContentType(string contentType, IEnumerable<string> allowedPrefixes)
    {
        return allowedPrefixes.Any(prefix =>
            contentType.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildRelativeFolder(Guid collectionId, AttachmentSlot slot)
    {
        return Path.Combine("collections", collectionId.ToString("N"), slot.ToString()).Replace('\\', '/');
    }

    private static AttachmentDto ToDto(AttachmentModel attachment)
    {
        return new AttachmentDto
        {
            AttachmentId = attachment.AttachmentId,
            AttachmentCollectionId = attachment.AttachmentCollectionId,
            Slot = attachment.Slot,
            FileName = attachment.FileName,
            SizeBytes = attachment.SizeBytes,
            Url = AttachmentFileHelper.BuildUrl(attachment.AttachmentId),
            DownloadUrl = AttachmentFileHelper.BuildDownloadUrl(attachment.AttachmentId),
            IsImage = AttachmentFileHelper.IsImageFile(attachment.FileName),
            CreateDate = attachment.CreateDate,
            CreateBy = attachment.CreateBy
        };
    }
}
