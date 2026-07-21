using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Enums.InternalMailEnums;

namespace HRM.Domain.Entities.InternalMailSchema;

public class InternalMessage
{
    public Guid InternalMessageId { get; set; }

    public Guid InternalConversationId { get; set; }
    public virtual InternalConversation Conversation { get; set; } = default!;

    public Guid SenderEmployeeId { get; set; }
    public virtual Employee SenderEmployee { get; set; } = default!;

    public InternalMessageType MessageType { get; set; } = InternalMessageType.Text;

    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// Metadata phu cho message he thong/action card. Body van la noi dung chinh de doc va search.
    /// </summary>
    public string? PayloadJson { get; set; }

    public Guid? ReplyToMessageId { get; set; }
    public virtual InternalMessage? ReplyToMessage { get; set; }

    public bool IsUrgent { get; set; }

    public DateTime SentAt { get; set; }

    /// <summary>
    /// Audit sua message. Neu cho phep edit noi dung thi service can set IsEdited, EditedAt va EditedByEmployeeId.
    /// </summary>
    public bool IsEdited { get; set; }
    public DateTime? EditedAt { get; set; }
    public Guid? EditedByEmployeeId { get; set; }

    /// <summary>
    /// Soft delete message trong thread, giu lai audit ai xoa va xoa luc nao.
    /// </summary>
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedByEmployeeId { get; set; }

    public virtual Employee? EditedByEmployeeNavigation { get; set; }
    public virtual Employee? DeletedByEmployeeNavigation { get; set; }

    public virtual ICollection<InternalMessage> Replies { get; set; } = new List<InternalMessage>();
    public virtual ICollection<InternalMessageAttachment> Attachments { get; set; } = new List<InternalMessageAttachment>();
    public virtual ICollection<InternalMessageReference> References { get; set; } = new List<InternalMessageReference>();
    public virtual ICollection<InternalMessageReadState> ReadStates { get; set; } = new List<InternalMessageReadState>();
}
