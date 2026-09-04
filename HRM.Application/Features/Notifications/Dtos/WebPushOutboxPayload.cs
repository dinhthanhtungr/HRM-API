namespace HRM.Application.Features.Notifications.Dtos;

/// <summary>
/// Danh sách employee đích chỉ gồm người nhận được phép nhận Web Push.
/// Payload lịch sử không có TargetEmployeeIds vẫn fallback theo NotificationUserState.
/// </summary>
public sealed class WebPushOutboxPayload
{
    public Guid NotificationId { get; set; }
    public IReadOnlyCollection<Guid>? TargetEmployeeIds { get; set; }
}
