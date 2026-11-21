namespace HRM.Application.Features.Notifications.Dtos;

public sealed record NotificationStreamPageDto(
    IReadOnlyList<NotificationDto> Items,
    Guid? FirstUnreadId,
    int UnreadCount,
    bool HasOlder,
    bool HasNewer);
