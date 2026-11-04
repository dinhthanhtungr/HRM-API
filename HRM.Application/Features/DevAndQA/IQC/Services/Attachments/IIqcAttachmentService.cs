using HRM.Application.Features.Attachments.Dtos;
using HRM.Domain.Enums.Attachment;

namespace HRM.Application.Features.DevAndQA.IQC.Services.Attachments;

/// <summary>Upload, đọc và xóa file theo dòng phiếu IQC, kiểm tra quyền trên QC cha.</summary>
public interface IIqcAttachmentService
{
    Task<IReadOnlyList<AttachmentDto>> UploadAsync(long voucherDetailId, AttachmentSlot slot, IReadOnlyList<AttachmentUploadFile> files, CancellationToken ct);
    Task<IReadOnlyList<AttachmentDto>> ListAsync(long voucherDetailId, AttachmentSlot? slot, CancellationToken ct);
    Task<AttachmentContent> GetContentAsync(long voucherDetailId, Guid attachmentId, CancellationToken ct);
    Task DeleteAsync(long voucherDetailId, Guid attachmentId, CancellationToken ct);
}
