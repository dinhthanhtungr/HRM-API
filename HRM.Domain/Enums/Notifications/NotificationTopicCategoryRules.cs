namespace HRM.Domain.Enums.Notifications;

/// <summary>
/// Applies the legacy-data cutoff and delegates current topic presentation to the catalog.
/// </summary>
public static class NotificationTopicCategoryRules
{
    public static readonly DateTime CurrentDataStartDate = new(2026, 7, 20);

    public static string GetCategoryCode(TopicNotifications topic, DateTime createdDate)
        => createdDate < CurrentDataStartDate
            ? NotificationCategoryCodes.LegacyData
            : NotificationTopicCatalog.GetDefinition(topic).CategoryCode;

    public static string GetEventGroupCode(TopicNotifications topic, DateTime createdDate)
        => createdDate < CurrentDataStartDate
            ? NotificationCategoryCodes.LegacyData
            : NotificationTopicCatalog.GetDefinition(topic).EventGroupCode;
}
