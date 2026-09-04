using HRM.Application.Features.Attachments.Dtos;
using HRM.Domain.Enums.Attachment;

namespace HRM.Application.Features.PLM.SampleRequests.Attachments;

public interface ISampleRequestAttachmentService
{
    Task<IReadOnlyList<AttachmentDto>> UploadListAsync(
        Guid sampleRequestId,
        AttachmentSlot slot,
        IReadOnlyList<AttachmentUploadFile> files,
        Guid? createdBy,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AttachmentDto>> ListAsync(
        Guid sampleRequestId,
        AttachmentSlot? slot,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid sampleRequestId,
        Guid attachmentId,
        Guid? deletedBy,
        CancellationToken cancellationToken = default);
}
