using System.Text.Json.Serialization;
using HRM.Domain.Enums.InternalMailEnums;

namespace HRM.Application.Features.InternalMail.Dtos;

/// <summary>
/// Mot dong conversation trong hom thu tong. Danh sach duoc gop theo conversation, khong gop/xoa message that.
/// </summary>
public sealed class InternalConversationListItemDto
{
    public Guid ConversationId { get; set; }
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// Tiêu đề ngắn dành cho danh sách inbox; Subject đầy đủ vẫn dùng ở màn hình chi tiết.
    /// </summary>
    public string DisplayTitle { get; set; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public InternalMailRelatedType? RelatedType { get; set; }

    public Guid? RelatedId { get; set; }
    public string? RelatedExternalId { get; set; }
    public Guid? LastMessageId { get; set; }
    public string? LastMessageBody { get; set; }
    public Guid? LastSenderEmployeeId { get; set; }
    public string? LastSenderName { get; set; }
    public DateTime LastMessageAt { get; set; }
    public int UnreadCount { get; set; }
    public bool IsUrgent { get; set; }
    public bool IsArchived { get; set; }
    public bool IsMuted { get; set; }
}

public sealed class InternalConversationDetailDto
{
    public Guid ConversationId { get; set; }
    public string Subject { get; set; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public InternalMailRelatedType? RelatedType { get; set; }

    public Guid? RelatedId { get; set; }
    public string? RelatedExternalId { get; set; }
    public Guid CreatedBy { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastMessageAt { get; set; }
    public Guid? LastMessageId { get; set; }
    public bool IsArchived { get; set; }
    public bool IsMuted { get; set; }
    public DateTime? LastReadAt { get; set; }
    public IReadOnlyList<InternalConversationParticipantDto> Participants { get; set; } = Array.Empty<InternalConversationParticipantDto>();
}

public sealed class InternalConversationParticipantDto
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public InternalConversationParticipantRole Role { get; set; }

    public DateTime JoinedAt { get; set; }
}
