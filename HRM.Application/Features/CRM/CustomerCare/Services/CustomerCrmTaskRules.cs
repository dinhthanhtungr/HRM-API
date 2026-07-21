using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.WorkTaskEnums;

namespace HRM.Application.Features.CRM.CustomerCare.Services;

/// <summary>
/// Các rule thuần dùng chung để nhận diện trạng thái task và chọn color key ổn định cho FE.
/// </summary>
internal static class CustomerCrmTaskRules
{
    public const int MaxTitleLength = 250;
    public const int MaxBodyLength = 8000;

    /// <summary>
    /// Xác định task còn đang mở và cần tiếp tục theo dõi.
    /// </summary>
    public static bool IsOpen(WorkTaskStatus status)
        => status is WorkTaskStatus.Pending or WorkTaskStatus.InProgress or WorkTaskStatus.Overdue;

    /// <summary>
    /// Xác định task đã kết thúc để loại khỏi các phép tính follow-up còn mở.
    /// </summary>
    public static bool IsTerminal(WorkTaskStatus status)
        => status is WorkTaskStatus.Done or WorkTaskStatus.Canceled;

    /// <summary>
    /// Chọn color key theo thứ tự ưu tiên quá hạn, trạng thái kết thúc và độ ưu tiên.
    /// </summary>
    public static EventTypeColorKey ResolveTaskColor(WorkTaskStatus status, WorkTaskPriority priority, bool overdue)
    {
        if (overdue) return EventTypeColorKey.Overdue;
        if (status == WorkTaskStatus.Done) return EventTypeColorKey.Done;
        if (status == WorkTaskStatus.Canceled) return EventTypeColorKey.Canceled;
        if (priority == WorkTaskPriority.Urgent) return EventTypeColorKey.Urgent;
        if (priority == WorkTaskPriority.High) return EventTypeColorKey.High;
        return EventTypeColorKey.FollowUpTask;
    }

    /// <summary>
    /// Chuyển loại interaction thành color key để FE tự ánh xạ màu hiển thị.
    /// </summary>
    public static EventTypeColorKey ResolveInteractionColor(CustomerInteractionType type)
        => type switch
        {
            CustomerInteractionType.Meeting => EventTypeColorKey.Meeting,
            CustomerInteractionType.Visit => EventTypeColorKey.Visit,
            CustomerInteractionType.Call => EventTypeColorKey.Call,
            _ => EventTypeColorKey.Interaction
        };
}
