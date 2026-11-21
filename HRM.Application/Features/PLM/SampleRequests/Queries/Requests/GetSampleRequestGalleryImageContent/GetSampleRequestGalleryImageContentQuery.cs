using HRM.Application.Features.Attachments.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestGalleryImageContent;

public sealed record GetSampleRequestGalleryImageContentQuery(
    Guid SampleRequestId, Guid AttachmentId, bool Thumbnail) : IRequest<AttachmentContent?>;
