using HRM.Application.Features.Attachments.Dtos;
using HRM.Application.Features.Purchasing.Catalog.Dtos;
using HRM.Domain.Enums.Attachment;

namespace HRM.Application.Features.Purchasing.Catalog.Services;

public interface IMaterialAttachmentService
{
    Task<MaterialAttachmentListDto> ListAsync(Guid materialId, CancellationToken ct);
    Task<AttachmentContent> ContentAsync(Guid materialId, Guid attachmentId, CancellationToken ct);
    Task<MaterialAttachmentListDto> UploadAsync(Guid materialId, AttachmentSlot slot,
        IReadOnlyList<AttachmentUploadFile> files, DateTime? expectedUpdatedDate, CancellationToken ct);
    Task<MaterialAttachmentListDto> DeleteAsync(Guid materialId, Guid attachmentId, DateTime? expectedUpdatedDate, CancellationToken ct);
}
