using HRM.Domain.Entities.AttachmentSchema;

namespace HRM.Domain.Entities.InternalMailSchema;

public class InternalMessageAttachment
{
    public Guid InternalMessageAttachmentId { get; set; }

    public Guid InternalMessageId { get; set; }
    public virtual InternalMessage Message { get; set; } = default!;

    public Guid AttachmentId { get; set; }
    public virtual AttachmentModel Attachment { get; set; } = default!;

    public DateTime AttachedAt { get; set; }
}
