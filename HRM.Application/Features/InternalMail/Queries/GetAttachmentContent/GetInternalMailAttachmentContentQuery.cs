using HRM.Application.Features.Attachments.Dtos;
using MediatR;

namespace HRM.Application.Features.InternalMail.Queries.GetAttachmentContent;

public sealed class GetInternalMailAttachmentContentQuery : IRequest<AttachmentContent?>
{
    public Guid AttachmentId { get; set; }
    /// <summary>
    /// Khi có giá trị, tệp được kiểm tra thêm là tệp chat hoặc tệp gốc Sample Request
    /// của conversation này. Dùng cho danh sách tệp liên quan.
    /// </summary>
    public Guid? ConversationId { get; set; }
    public bool Thumbnail { get; set; }
}
