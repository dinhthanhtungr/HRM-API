namespace HRM.Application.Features.Notifications.Dtos;

public sealed record NotificationStreamPageDto(
    IReadOnlyList<NotificationDto> Items,
    Guid? FirstUnreadId,
    int UnreadCount,
    bool HasOlder,
    bool HasNewer)
{
    public IReadOnlyList<StreamUnreadItemDto>? OpeningUnread { get; init; }
}

public sealed record StreamUnreadItemDto(Guid Id, string EventGroupCode);
