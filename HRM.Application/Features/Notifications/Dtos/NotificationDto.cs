using System.Text.Json.Serialization;
using HRM.Domain.Enums.Notifications;

namespace HRM.Application.Features.Notifications.Dtos;

/// <summary>
/// Notification trả về cho FE ở feed/detail, kèm trạng thái đã đọc của nhân viên hiện tại.
/// </summary>
public sealed class NotificationDto
{
    public Guid Id { get; set; }

    public TopicNotifications Topic { get; set; }

    /// <summary>
    /// Mã topic phân cấp ổn định cho frontend; không được lưu thành cột database.
    /// </summary>
    public string TopicCode => NotificationTopicCodes.GetCode(Topic);

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public NotificationCategory Category { get; set; }

    public NotificationSeverity Severity { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string? Link { get; set; }

    public string? PayloadJson { get; set; }

    public DateTime CreatedDate { get; set; }

    public Guid CompanyId { get; set; }

    public bool IsRead { get; set; }

    public DateTime? ReadDate { get; set; }

    public Guid? CreatedBy { get; set; }

    public string? CreatedByNameSnapshot { get; set; }
}
