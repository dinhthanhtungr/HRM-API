namespace HRM.Domain.Enums.Notifications;

/// <summary>
/// Ánh xạ topic legacy sang mã phân cấp ổn định để frontend hiển thị, điều hướng và lọc.
/// TopicNotifications vẫn là giá trị được lưu trong database; mã này chỉ thuộc contract API.
/// </summary>
public static class NotificationTopicCodes
{
    public static string GetCode(TopicNotifications topic)
        => NotificationTopicCatalog.GetDefinition(topic).Code;
}
