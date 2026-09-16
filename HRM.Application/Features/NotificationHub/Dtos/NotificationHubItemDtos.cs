using HRM.Application.Features.InternalMail.Dtos;
using HRM.Application.Features.Notifications.Dtos;

namespace HRM.Application.Features.NotificationHub.Dtos;

/// <summary>
/// Một trang read-model dành riêng cho Notification Hub. Cursor tiếp theo giữ nguyên
/// thứ tự keyset của notification feed; null khi trang hiện tại chưa đầy.
/// </summary>
public sealed class NotificationHubItemsDto
{
    public IReadOnlyList<NotificationHubItemDto> Items { get; set; } = Array.Empty<NotificationHubItemDto>();
    public string? NextCursor { get; set; }
}

/// <summary>
/// Ghép một notification với metadata conversation mà current employee còn quyền đọc.
/// Notification không có conversation hợp lệ vẫn được trả với ConversationInfo = null.
/// </summary>
public sealed class NotificationHubItemDto
{
    public NotificationDto Notification { get; set; } = new();
    public NotificationHubConversationInfoDto? ConversationInfo { get; set; }
}

/// <summary>
/// Snapshot hiển thị hiện tại của conversation. Last-message và unread là trạng thái
/// toàn thread, không phải trạng thái riêng của event group đang lọc.
/// </summary>
public sealed class NotificationHubConversationInfoDto
{
    public string? DisplayTitle { get; set; }
    public string? LastSenderName { get; set; }
    public string? LastMessageBody { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public int UnreadCount { get; set; }
    public bool IsUrgent { get; set; }
    public SampleRequestConversationInfoDto? SampleRequestInfo { get; set; }
    public QuotationConversationInfoDto? QuotationInfo { get; set; }
}
