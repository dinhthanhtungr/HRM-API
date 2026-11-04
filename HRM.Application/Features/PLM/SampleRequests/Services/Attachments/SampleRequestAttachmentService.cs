using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.Attachments.Dtos;
using HRM.Application.Features.Attachments.Services;
using HRM.Domain.Entities.AuditSchema;
using HRM.Domain.Enums.Audits;
using HRM.Domain.Enums.Attachment;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace HRM.Application.Features.PLM.SampleRequests.Attachments;

internal sealed class SampleRequestAttachmentService : ISampleRequestAttachmentService
{
    private const string SampleRequestsAuditSchema = "SampleRequests";
    private const string AttachmentsAuditTable = "Attachments";
    private const string UploadAuditReason = "SampleRequestAttachmentUploaded";
    private const string DeleteAuditReason = "SampleRequestAttachmentDeleted";

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
        var sampleRequest = await GetSampleRequestAuditTargetAsync(sampleRequestId, cancellationToken);

        var attachments = await _attachmentService.UploadListAsync(
            sampleRequest.AttachmentCollectionId,
            slot,
            files,
            createdBy,
            cancellationToken);

        if (attachments.Count == 0)
        {
            return attachments;
        }

        var changedAt = DateTime.Now;
        var correlationId = Guid.CreateVersion7();
        foreach (var attachment in attachments)
        {
            AddAttachmentAudit(
                sampleRequest,
                attachment.AttachmentId,
                attachment.FileName,
                attachment.Slot,
                AuditActionType.Create,
                createdBy,
                changedAt,
                correlationId,
                UploadAuditReason);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return attachments;
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
        Guid? deletedBy,
        CancellationToken cancellationToken = default)
    {
        var sampleRequest = await GetSampleRequestAuditTargetAsync(sampleRequestId, cancellationToken);

        var attachment = await _dbContext.AttachmentModels
            .AsNoTracking()
            .Where(
                x => x.AttachmentId == attachmentId &&
                    x.AttachmentCollectionId == sampleRequest.AttachmentCollectionId &&
                    x.IsActive)
            .Select(x => new { x.AttachmentId, x.FileName, x.Slot })
            .FirstOrDefaultAsync(cancellationToken);

        if (attachment is null)
        {
            throw new FileNotFoundException("Attachment was not found for this sample request.");
        }

        await _attachmentService.DeleteAsync(attachmentId, cancellationToken);
        AddAttachmentAudit(
            sampleRequest,
            attachment.AttachmentId,
            attachment.FileName,
            attachment.Slot,
            AuditActionType.Delete,
            deletedBy,
            DateTime.Now,
            Guid.CreateVersion7(),
            DeleteAuditReason);
        await _dbContext.SaveChangesAsync(cancellationToken);
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

    private async Task<SampleRequestAttachmentAuditTarget> GetSampleRequestAuditTargetAsync(
        Guid sampleRequestId,
        CancellationToken cancellationToken)
    {
        if (sampleRequestId == Guid.Empty)
        {
            throw new InvalidOperationException("SampleRequestId is invalid.");
        }

        var sampleRequest = await _dbContext.SampleRequests
            .AsNoTracking()
            .Where(x => x.SampleRequestId == sampleRequestId && x.IsActive)
            .Select(x => new SampleRequestAttachmentAuditTarget(
                x.SampleRequestId,
                x.AttachmentCollectionId,
                x.CompanyId))
            .FirstOrDefaultAsync(cancellationToken);

        return sampleRequest
            ?? throw new KeyNotFoundException("Sample request was not found.");
    }

    private void AddAttachmentAudit(
        SampleRequestAttachmentAuditTarget sampleRequest,
        Guid attachmentId,
        string fileName,
        AttachmentSlot slot,
        AuditActionType actionType,
        Guid? changedBy,
        DateTime changedAt,
        Guid correlationId,
        string reason)
    {
        var values = new
        {
            AttachmentId = attachmentId,
            FileName = fileName,
            Slot = slot.ToString()
        };
        var changedValues = new Dictionary<string, object?>
        {
            ["FileName"] = new
            {
                Old = actionType == AuditActionType.Create ? null : fileName,
                New = actionType == AuditActionType.Create ? fileName : null
            }
        };

        _dbContext.AuditLogs.Add(new AuditLog
        {
            AuditLogId = Guid.CreateVersion7(),
            CompanyId = sampleRequest.CompanyId,
            SchemaName = SampleRequestsAuditSchema,
            TableName = AttachmentsAuditTable,
            RecordId = sampleRequest.SampleRequestId,
            ActionType = actionType,
            ChangedBy = changedBy,
            ChangedAt = changedAt,
            OldValues = actionType == AuditActionType.Delete ? JsonSerializer.SerializeToDocument(values) : null,
            NewValues = actionType == AuditActionType.Create ? JsonSerializer.SerializeToDocument(values) : null,
            ChangedValues = JsonSerializer.SerializeToDocument(changedValues),
            Reason = reason,
            CorrelationId = correlationId
        });
    }

    private sealed record SampleRequestAttachmentAuditTarget(
        Guid SampleRequestId,
        Guid AttachmentCollectionId,
        Guid? CompanyId);
}
