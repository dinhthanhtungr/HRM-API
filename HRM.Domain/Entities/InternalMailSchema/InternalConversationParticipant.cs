using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Enums.InternalMailEnums;

namespace HRM.Domain.Entities.InternalMailSchema;

public class InternalConversationParticipant
{
    public Guid InternalConversationId { get; set; }
    public virtual InternalConversation Conversation { get; set; } = default!;

    public Guid EmployeeId { get; set; }
    public virtual Employee Employee { get; set; } = default!;

    /// <summary>
    /// Vai tro cua nhan vien trong conversation, dung cho quyen quan ly thread o cac phase sau.
    /// </summary>
    public InternalConversationParticipantRole Role { get; set; } = InternalConversationParticipantRole.Member;

    /// <summary>
    /// Archive ca nhan theo tung participant, khong xoa conversation cho nguoi khac.
    /// </summary>
    public bool IsArchived { get; set; }
    public DateTime? ArchivedAt { get; set; }

    /// <summary>
    /// Moc doc nhanh theo conversation de tinh unread cho inbox; read-state chi tiet nam o InternalMessageReadState.
    /// </summary>
    public DateTime? LastReadAt { get; set; }

    public DateTime JoinedAt { get; set; }

    public bool IsMuted { get; set; }
}
