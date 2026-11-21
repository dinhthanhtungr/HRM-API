using HRM.Application.Abstractions.FileStorage;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.Attachments.Dtos;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestGallery;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestGalleryImageContent;

internal sealed class GetSampleRequestGalleryImageContentQueryHandler(
    IPLMReadDbContext db, ICustomerVisibilityService visibility,
    IFileStorage storage, IImageThumbnailGenerator thumbnails)
    : IRequestHandler<GetSampleRequestGalleryImageContentQuery, AttachmentContent?>
{
    public async Task<AttachmentContent?> Handle(GetSampleRequestGalleryImageContentQuery request, CancellationToken ct)
    {
        var requests = await SampleRequestGalleryAccess.ReadableAsync(db, visibility, request.SampleRequestId, ct);
        var image = await SampleRequestGalleryImages.Eligible(db.AttachmentModels.AsNoTracking())
            .Where(a => a.AttachmentId == request.AttachmentId &&
                requests.Any(r => r.AttachmentCollectionId == a.AttachmentCollectionId))
            .FirstOrDefaultAsync(ct);
        // Kiểm tra quyền và liên kết cha trước mọi lần đọc cache/file.
        if (image is null) return null;
        try
        {
            if (!request.Thumbnail)
            {
                var original = await storage.OpenReadAsync(image.StoragePath, ct);
                return new AttachmentContent(original.Stream, original.ContentType, image.FileName, original.Length);
            }
            // Tệp cũ có thể thiếu size metadata; kích thước stream mới là kiểm tra cuối.
            if (image.SizeBytes > GalleryThumbnailStorage.MaxSourceBytes) return null;
            var path = GalleryThumbnailStorage.PathFor(image);
            var gate = GalleryThumbnailStorage.Gate(image.AttachmentId);
            await gate.WaitAsync(ct);
            try
            {
                try
                {
                    var cached = await storage.OpenReadAsync(path, ct);
                    return new AttachmentContent(cached.Stream, "image/webp", "preview.webp", cached.Length);
                }
                catch (FileNotFoundException) { }

                await GalleryThumbnailStorage.GenerationSlots.WaitAsync(ct);
                try
                {
                    var source = await storage.OpenReadAsync(image.StoragePath, ct);
                    await using var input = source.Stream;
                    if (source.Length <= 0 || source.Length > GalleryThumbnailStorage.MaxSourceBytes) return null;
                    await using var generated = await thumbnails.GenerateWebpAsync(input,
                        GalleryThumbnailStorage.MaxEdge, GalleryThumbnailStorage.MaxEdge, ct);
                    if (generated is null) return null;
                    try
                    {
                        await storage.SaveAtPathAsync(generated, path, ct);
                    }
                    catch
                    {
                        // Không để lần ghi bị hủy/lỗi trở thành thumbnail hỏng ở lần đọc sau.
                        await storage.DeleteAsync(path, CancellationToken.None);
                        throw;
                    }
                    var saved = await storage.OpenReadAsync(path, ct);
                    return new AttachmentContent(saved.Stream, "image/webp", "preview.webp", saved.Length);
                }
                finally { GalleryThumbnailStorage.GenerationSlots.Release(); }
            }
            finally { gate.Release(); }
        }
        catch (FileNotFoundException) { return null; }
    }
}
