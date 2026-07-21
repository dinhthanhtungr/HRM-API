namespace HRM.Domain.Enums.Notifications;

/// <summary>
/// Ánh xạ topic legacy sang mã phân cấp ổn định để frontend hiển thị, điều hướng và lọc.
/// TopicNotifications vẫn là giá trị được lưu trong database; mã này chỉ thuộc contract API.
/// </summary>
public static class NotificationTopicCodes
{
    public static string GetCode(TopicNotifications topic)
    {
        return topic switch
        {
            TopicNotifications.ProductSampleCreated => "plm.product_sample.created",
            TopicNotifications.ProductSampleUpdated => "plm.product_sample.updated",
            TopicNotifications.ProductSampleDeleted => "plm.product_sample.deleted",
            TopicNotifications.SampleRequestUpdated => "plm.sample_request.updated",

            TopicNotifications.MerchandiseOrderCreated => "sales.merchandise_order.created",
            TopicNotifications.MerchandiseOrderUpdated => "sales.merchandise_order.updated",
            TopicNotifications.MerchandiseOrderDeleted => "sales.merchandise_order.deleted",

            TopicNotifications.ManufacturingOrderCreated => "mfg.manufacturing_order.created",
            TopicNotifications.ManufacturingOrderUpdated => "mfg.manufacturing_order.updated",
            TopicNotifications.ManufacturingOrderDeleted => "mfg.manufacturing_order.deleted",

            TopicNotifications.PriceOverSellCreated => "sales.price_over_sell.created",
            TopicNotifications.WarehouseStockLost => "warehouse.stock.lost",

            TopicNotifications.CustomerLeadAssigned => "crm.customer.lead.assigned",
            TopicNotifications.CustomerFollowUpTaskAssigneeAdded => "crm.customer.follow_up.assignee_added",
            TopicNotifications.CustomerFollowUpTaskDue => "crm.customer.follow_up.due",

            TopicNotifications.WorkTaskAssigneeAdded => "work.task.assignee_added",
            TopicNotifications.WorkTaskDue => "work.task.due",
            TopicNotifications.WorkPlanAssigneeAdded => "work.plan.assignee_added",

            TopicNotifications.MfgProductionOrderChangeExpectiveDate => "mfg.production_order.expected_date_changed",
            TopicNotifications.MfgProductionOrderUpdated => "mfg.production_order.updated",
            TopicNotifications.MfgProductionOrderDeleted => "mfg.production_order.deleted",
            TopicNotifications.ManufacturingFormulaAdjustmentCreated => "mfg.formula_adjustment.created",

            TopicNotifications.SampleRequestMessageCreated => "plm.sample_request.message.created",
            TopicNotifications.SampleRequestPriceQuoteRequested => "plm.sample_request.price_quote.requested",
            TopicNotifications.SampleRequestChangeRequested => "plm.sample_request.change.requested",
            TopicNotifications.SampleRequestUpdateRequested => "plm.sample_request.update.requested",

            TopicNotifications.InternalMailMessageCreated => "internal_mail.message.created",
            _ => $"notification.unknown.{(int)topic}"
        };
    }
}
