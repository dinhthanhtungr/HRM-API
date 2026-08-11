using HRM.Application.Features.Attachments.Dtos;
using MediatR;

namespace HRM.Application.Features.InternalMail.Queries.GetAttachmentContent;

public sealed class GetInternalMailAttachmentContentQuery : IRequest<AttachmentContent?>
{
    public Guid AttachmentId { get; set; }
    public bool Thumbnail { get; set; }
}
