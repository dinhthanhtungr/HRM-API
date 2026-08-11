namespace HRM.Application.Features.Notifications.Dtos;

/// <summary>
/// Unread totals grouped by business category and event group for the current employee.
/// </summary>
public sealed class NotificationUnreadSummaryDto
{
    public int TotalUnread { get; set; }

    public IReadOnlyList<NotificationCategoryUnreadCountDto> Categories { get; set; }
        = Array.Empty<NotificationCategoryUnreadCountDto>();
}

public sealed class NotificationCategoryUnreadCountDto
{
    public string CategoryCode { get; set; } = string.Empty;

    public int UnreadCount { get; set; }

    public IReadOnlyList<NotificationEventGroupUnreadCountDto> EventGroups { get; set; }
        = Array.Empty<NotificationEventGroupUnreadCountDto>();
}

public sealed class NotificationEventGroupUnreadCountDto
{
    public string EventGroupCode { get; set; } = string.Empty;

    public int UnreadCount { get; set; }
}
