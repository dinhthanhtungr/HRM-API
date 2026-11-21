using HRM.Application.Features.PLM.SampleRequests.Dtos.Gallery;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestGalleryImages;

public sealed record GetSampleRequestGalleryImagesQuery(Guid SampleRequestId)
    : IRequest<IReadOnlyList<SampleRequestGalleryImageDto>?>;
