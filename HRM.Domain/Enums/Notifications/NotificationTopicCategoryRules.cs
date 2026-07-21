namespace HRM.Domain.Enums.Notifications;

/// <summary>
/// Anh xa topic chi tiet sang nhom loc on dinh cho FE. Label hien thi do FE map tu NotificationCategory.
/// </summary>
public static class NotificationTopicCategoryRules
{
    /// <summary>
    /// Notification tạo trước thời điểm này được gom vào nhóm dữ liệu cũ khi trả API.
    /// </summary>
    public static readonly DateTime CurrentDataStartDate = new(2026, 7, 20);

    public static NotificationCategory GetCategory(
        TopicNotifications topic,
        DateTime createdDate)
    {
        return createdDate < CurrentDataStartDate
            ? NotificationCategory.LegacyData
            : GetCategory(topic);
    }

    public static NotificationCategory GetCategory(TopicNotifications topic)
    {
        return topic switch
        {
            TopicNotifications.ProductSampleCreated or
            TopicNotifications.ProductSampleUpdated or
            TopicNotifications.ProductSampleDeleted or
            TopicNotifications.SampleRequestUpdated or
            TopicNotifications.SampleRequestMessageCreated or
            TopicNotifications.SampleRequestPriceQuoteRequested or
            TopicNotifications.SampleRequestChangeRequested or
            TopicNotifications.SampleRequestUpdateRequested => NotificationCategory.SampleRequest,

            TopicNotifications.MerchandiseOrderCreated or
            TopicNotifications.MerchandiseOrderUpdated or
            TopicNotifications.MerchandiseOrderDeleted => NotificationCategory.MerchandiseOrder,

            TopicNotifications.ManufacturingOrderCreated or
            TopicNotifications.ManufacturingOrderUpdated or
            TopicNotifications.ManufacturingOrderDeleted or
            TopicNotifications.MfgProductionOrderChangeExpectiveDate or
            TopicNotifications.MfgProductionOrderUpdated or
            TopicNotifications.MfgProductionOrderDeleted or
            TopicNotifications.ManufacturingFormulaAdjustmentCreated => NotificationCategory.Manufacturing,

            TopicNotifications.CustomerLeadAssigned or
            TopicNotifications.CustomerFollowUpTaskAssigneeAdded or
            TopicNotifications.CustomerFollowUpTaskDue => NotificationCategory.Customer,

            TopicNotifications.WorkTaskAssigneeAdded or
            TopicNotifications.WorkTaskDue or
            TopicNotifications.WorkPlanAssigneeAdded => NotificationCategory.Work,

            TopicNotifications.InternalMailMessageCreated => NotificationCategory.InternalMail,
            _ => NotificationCategory.System
        };
    }

    public static IReadOnlyCollection<TopicNotifications> GetTopics(NotificationCategory category)
    {
        return Enum.GetValues<TopicNotifications>()
            .Where(topic => GetCategory(topic) == category)
            .ToArray();
    }
}
