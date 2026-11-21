using HRM.Application.Features.PLM.SampleRequests.Dtos.Gallery;
using HRM.Domain.Entities.AttachmentSchema;
using HRM.Domain.Enums.Attachment;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestGallery;

internal static class SampleRequestGalleryImages
{
    // Chỉ ảnh yêu cầu, không lấy ảnh hội thoại hoặc tài liệu nghiệp vụ khác.
    public static IQueryable<AttachmentModel> Eligible(IQueryable<AttachmentModel> query) =>
        query.Where(x => x.IsActive && x.Slot == AttachmentSlot.SampleRequest &&
            (x.FileName.ToLower().EndsWith(".jpg") || x.FileName.ToLower().EndsWith(".jpeg") ||
             x.FileName.ToLower().EndsWith(".png") || x.FileName.ToLower().EndsWith(".webp") ||
             x.FileName.ToLower().EndsWith(".gif") || x.FileName.ToLower().EndsWith(".bmp")));

    public static SampleRequestGalleryImageDto ToDto(Guid requestId, Guid attachmentId, string fileName)
    {
        var route = $"/api/v1/plm/sample-requests/{requestId}/attachments/{attachmentId}";
        return new SampleRequestGalleryImageDto
        {
            AttachmentId = attachmentId,
            FileName = fileName,
            Url = $"{route}/gallery-content",
            ThumbnailUrl = $"{route}/thumbnail"
        };
    }
}
