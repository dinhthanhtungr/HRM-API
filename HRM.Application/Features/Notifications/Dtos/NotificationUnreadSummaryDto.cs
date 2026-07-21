using System.Text.Json.Serialization;
using HRM.Domain.Enums.Notifications;

namespace HRM.Application.Features.Notifications.Dtos;

/// <summary>
/// Tổng số notification chưa đọc và số lượng theo từng nhóm nghiệp vụ cho nhân viên hiện tại.
/// </summary>
public sealed class NotificationUnreadSummaryDto
{
    public int TotalUnread { get; set; }

    public IReadOnlyList<NotificationCategoryUnreadCountDto> Categories { get; set; }
        = Array.Empty<NotificationCategoryUnreadCountDto>();
}

/// <summary>
/// Số notification chưa đọc của một category; category có count bằng 0 vẫn được trả về.
/// </summary>
public sealed class NotificationCategoryUnreadCountDto
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public NotificationCategory Category { get; set; }

    public int UnreadCount { get; set; }
}
