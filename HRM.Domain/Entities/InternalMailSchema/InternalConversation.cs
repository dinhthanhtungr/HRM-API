using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Enums.InternalMailEnums;

namespace HRM.Domain.Entities.InternalMailSchema;

public class InternalConversation
{
    public Guid InternalConversationId { get; set; }

    public Guid CompanyId { get; set; }

    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// Loai nghiep vu ma conversation dang bam theo. RelatedId la khoa ngoai mem nen service can tu validate entity dich.
    /// </summary>
    public InternalMailRelatedType? RelatedType { get; set; }
    public Guid? RelatedId { get; set; }
    public string? RelatedExternalId { get; set; }

    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }

    public DateTime LastMessageAt { get; set; }
    public Guid? LastMessageId { get; set; }

    /// <summary>
    /// Soft delete toan conversation cho toan he thong. Archive rieng tung nguoi nam o InternalConversationParticipant.IsArchived.
    /// </summary>
    public bool IsActive { get; set; } = true;
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedByEmployeeId { get; set; }

    public virtual Company Company { get; set; } = default!;
    public virtual Employee CreatedByNavigation { get; set; } = default!;
    public virtual Employee? DeletedByEmployeeNavigation { get; set; }
    public virtual InternalMessage? LastMessage { get; set; }

    public virtual ICollection<InternalConversationParticipant> Participants { get; set; } = new List<InternalConversationParticipant>();
    public virtual ICollection<InternalMessage> Messages { get; set; } = new List<InternalMessage>();
}
