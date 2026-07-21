using HRM.Application.Features.Attachments.Dtos;
using HRM.Domain.Enums.Attachment;

namespace HRM.Application.Features.Attachments.Services;

public interface IAttachmentService
{
    /// <summary>
    /// Lưu file vật lý + tạo AttachmentModel.
    /// </summary>
    /// <param name="collectionId"></param>
    /// <param name="slot"></param>
    /// <param name="files"></param>
    /// <param name="createdBy"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IReadOnlyList<AttachmentDto>> UploadListAsync(
        Guid collectionId,
        AttachmentSlot slot,
        IReadOnlyList<AttachmentUploadFile> files,
        Guid? createdBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy danh sách file trong một AttachmentCollection.
    /// </summary>
    /// <param name="collectionId"></param>
    /// <param name="slot"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IReadOnlyList<AttachmentDto>> ListAsync(
        Guid collectionId,
        AttachmentSlot? slot,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Mở stream file để xem/download.
    /// </summary>
    /// <param name="attachmentId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<AttachmentContent> GetContentAsync(Guid attachmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft delete: IsActive = false.
    /// </summary>
    /// <param name="attachmentId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task DeleteAsync(Guid attachmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Xóa record + xóa file vật lý.
    /// </summary>
    /// <param name="attachmentId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task HardDeleteAsync(Guid attachmentId, CancellationToken cancellationToken = default);
}
