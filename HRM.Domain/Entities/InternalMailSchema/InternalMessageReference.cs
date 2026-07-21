using HRM.Domain.Enums.InternalMailEnums;

namespace HRM.Domain.Entities.InternalMailSchema;

public class InternalMessageReference
{
    public Guid InternalMessageReferenceId { get; set; }

    public Guid InternalMessageId { get; set; }
    public virtual InternalMessage Message { get; set; } = default!;

    public InternalMailRelatedType RelatedType { get; set; }
    public Guid RelatedId { get; set; }

    public string? RelatedExternalId { get; set; }
    public string? RelatedNameSnapshot { get; set; }

    /// <summary>
    /// Snapshot JSON phu tai thoi diem gui message, dung de render card/link ma khong phu thuoc ten hien tai cua entity dich.
    /// </summary>
    public string? SnapshotJson { get; set; }

    public bool IsPrimary { get; set; }
}
