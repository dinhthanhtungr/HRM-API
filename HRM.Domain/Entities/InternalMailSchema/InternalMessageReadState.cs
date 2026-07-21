using HRM.Domain.Entities.HrSchema;

namespace HRM.Domain.Entities.InternalMailSchema;

/// <summary>
/// Trang thai doc chi tiet theo tung message va tung nhan vien; khac voi LastReadAt la moc doc nhanh theo conversation.
/// </summary>
public class InternalMessageReadState
{
    public Guid InternalMessageId { get; set; }
    public virtual InternalMessage Message { get; set; } = default!;

    public Guid EmployeeId { get; set; }
    public virtual Employee Employee { get; set; } = default!;

    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
}
