using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Enums.InternalMailEnums;

namespace HRM.Domain.Entities.InternalMailSchema;

/// <summary>Mỗi nhân viên có tối đa một cảm xúc trên một tin nhắn; không thay đổi trạng thái đọc.</summary>
public class InternalMessageReaction
{
    public Guid MessageId { get; set; }
    public virtual InternalMessage Message { get; set; } = default!;
    public Guid EmployeeId { get; set; }
    public virtual Employee Employee { get; set; } = default!;
    public InternalMessageReactionType ReactionType { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
