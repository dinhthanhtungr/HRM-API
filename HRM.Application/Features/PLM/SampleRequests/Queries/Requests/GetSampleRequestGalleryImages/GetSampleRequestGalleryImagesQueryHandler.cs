using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.SampleRequests.Dtos.Gallery;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestGallery;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestGalleryImages;

internal sealed class GetSampleRequestGalleryImagesQueryHandler(
    IPLMReadDbContext db, ICustomerVisibilityService visibility)
    : IRequestHandler<GetSampleRequestGalleryImagesQuery, IReadOnlyList<SampleRequestGalleryImageDto>?>
{
    public async Task<IReadOnlyList<SampleRequestGalleryImageDto>?> Handle(
        GetSampleRequestGalleryImagesQuery request, CancellationToken ct)
    {
        var requests = await SampleRequestGalleryAccess.ReadableAsync(db, visibility, request.SampleRequestId, ct);
        var collection = await requests.Select(x => (Guid?)x.AttachmentCollectionId).FirstOrDefaultAsync(ct);
        if (!collection.HasValue) return null;
        var images = await SampleRequestGalleryImages.Eligible(db.AttachmentModels.AsNoTracking())
            .Where(x => x.AttachmentCollectionId == collection.Value)
            .OrderBy(x => x.CreateDate).ThenBy(x => x.AttachmentId)
            .Select(x => new { x.AttachmentId, x.FileName }).ToListAsync(ct);
        return images.Select(x => SampleRequestGalleryImages.ToDto(request.SampleRequestId, x.AttachmentId, x.FileName)).ToList();
    }
}
