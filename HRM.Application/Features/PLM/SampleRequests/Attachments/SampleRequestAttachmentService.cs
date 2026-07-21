using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.Attachments.Dtos;
using HRM.Application.Features.Attachments.Services;
using HRM.Domain.Enums.Attachment;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Attachments;

internal sealed class SampleRequestAttachmentService : ISampleRequestAttachmentService
{
    private readonly IPLMWriteDbContext _dbContext;
    private readonly IAttachmentService _attachmentService;

    public SampleRequestAttachmentService(
        IPLMWriteDbContext dbContext,
        IAttachmentService attachmentService)
    {
        _dbContext = dbContext;
        _attachmentService = attachmentService;
    }

    public async Task<IReadOnlyList<AttachmentDto>> UploadListAsync(
        Guid sampleRequestId,
        AttachmentSlot slot,
        IReadOnlyList<AttachmentUploadFile> files,
        Guid? createdBy,
        CancellationToken cancellationToken = default)
    {
        var collectionId = await GetAttachmentCollectionIdAsync(sampleRequestId, cancellationToken);

        return await _attachmentService.UploadListAsync(
            collectionId,
            slot,
            files,
            createdBy,
            cancellationToken);
    }

    public async Task<IReadOnlyList<AttachmentDto>> ListAsync(
        Guid sampleRequestId,
        AttachmentSlot? slot,
        CancellationToken cancellationToken = default)
    {
        var collectionId = await GetAttachmentCollectionIdAsync(sampleRequestId, cancellationToken);

        return await _attachmentService.ListAsync(collectionId, slot, cancellationToken);
    }

    public async Task DeleteAsync(
        Guid sampleRequestId,
        Guid attachmentId,
        CancellationToken cancellationToken = default)
    {
        var collectionId = await GetAttachmentCollectionIdAsync(sampleRequestId, cancellationToken);

        var belongsToSampleRequest = await _dbContext.AttachmentModels
            .AsNoTracking()
            .AnyAsync(
                x => x.AttachmentId == attachmentId &&
                    x.AttachmentCollectionId == collectionId &&
                    x.IsActive,
                cancellationToken);

        if (!belongsToSampleRequest)
        {
            throw new FileNotFoundException("Attachment was not found for this sample request.");
        }

        await _attachmentService.DeleteAsync(attachmentId, cancellationToken);
    }

    private async Task<Guid> GetAttachmentCollectionIdAsync(
        Guid sampleRequestId,
        CancellationToken cancellationToken)
    {
        if (sampleRequestId == Guid.Empty)
        {
            throw new InvalidOperationException("SampleRequestId is invalid.");
        }

        var collectionId = await _dbContext.SampleRequests
            .AsNoTracking()
            .Where(x => x.SampleRequestId == sampleRequestId && x.IsActive)
            .Select(x => (Guid?)x.AttachmentCollectionId)
            .FirstOrDefaultAsync(cancellationToken);

        return collectionId
            ?? throw new KeyNotFoundException("Sample request was not found.");
    }
}
