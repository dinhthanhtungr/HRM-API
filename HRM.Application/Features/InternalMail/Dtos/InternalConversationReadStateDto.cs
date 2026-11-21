namespace HRM.Application.Features.InternalMail.Dtos;

/// <summary>Trạng thái nhỏ sau xác nhận đọc; không thay thế metadata/quyền của conversation detail.</summary>
public sealed record InternalConversationReadStateDto(
    Guid ConversationId,
    string AreaCode,
    Guid? ThroughMessageId,
    int UnreadCount,
    Guid? LastMessageId,
    DateTime? LastMessageAt,
    IReadOnlyList<InternalConversationAreaUnreadDto> AreaUnreadCounts);

public sealed record InternalConversationAreaUnreadDto(Guid ConversationId, int UnreadCount);
