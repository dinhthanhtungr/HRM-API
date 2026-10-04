namespace HRM.Application.Features.NotificationHub.Dtos;

/// <summary>
/// Số bản ghi được cập nhật khi current employee đánh dấu toàn bộ Notification Hub đã đọc.
/// </summary>
public sealed class NotificationHubMarkAllReadResultDto
{
    public int NotificationsUpdated { get; init; }
    public int MessagesUpdated { get; init; }
    public int ConversationsUpdated { get; init; }
}
