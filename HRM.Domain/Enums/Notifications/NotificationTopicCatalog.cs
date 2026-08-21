namespace HRM.Domain.Enums.Notifications;

/// <summary>
/// Public presentation contract of a notification topic.
/// Topic remains the legacy database identifier; the other fields organize the Notification Hub.
/// </summary>
public readonly record struct NotificationTopicDefinition(
    string Code,
    string CategoryCode,
    string EventGroupCode,
    string? AggregateType);

/// <summary>
/// Single source of truth for topic code, business category, event group and aggregate type.
/// </summary>
public static class NotificationTopicCatalog
{
    private const string UnknownCodePrefix = "notification.unknown.";

    public static NotificationTopicDefinition GetDefinition(TopicNotifications topic)
    {
        return topic switch
        {
            TopicNotifications.ProductSampleCreated => SampleRequest("plm.product_sample.created", "sample", "ProductSample"),
            TopicNotifications.ProductSampleUpdated => SampleRequest("plm.product_sample.updated", "sample", "ProductSample"),
            TopicNotifications.ProductSampleDeleted => SampleRequest("plm.product_sample.deleted", "sample", "ProductSample"),
            TopicNotifications.SampleRequestUpdated => SampleRequest("plm.sample_request.updated", "change"),

            TopicNotifications.MerchandiseOrderCreated => SalesOrder("sales.merchandise_order.created", "lifecycle"),
            TopicNotifications.MerchandiseOrderUpdated => SalesOrder("sales.merchandise_order.updated", "change"),
            TopicNotifications.MerchandiseOrderDeleted => SalesOrder("sales.merchandise_order.deleted", "lifecycle"),

            TopicNotifications.ManufacturingOrderCreated => Production("mfg.manufacturing_order.created", "lifecycle", "ManufacturingOrder"),
            TopicNotifications.ManufacturingOrderUpdated => Production("mfg.manufacturing_order.updated", "change", "ManufacturingOrder"),
            TopicNotifications.ManufacturingOrderDeleted => Production("mfg.manufacturing_order.deleted", "lifecycle", "ManufacturingOrder"),

            TopicNotifications.PriceOverSellCreated => System("sales.price_over_sell.created", "pricing", "PriceOverSell"),
            TopicNotifications.WarehouseStockLost => Warehouse("warehouse.stock.lost", "inventory", "WarehouseStock"),

            TopicNotifications.CustomerLeadAssigned => Customer("crm.customer.lead.assigned", "assignment", "CustomerLead"),
            TopicNotifications.CustomerFollowUpTaskAssigneeAdded => Customer("crm.customer.follow_up.assignee_added", "follow_up", "WorkTask"),
            TopicNotifications.CustomerFollowUpTaskDue => Customer("crm.customer.follow_up.due", "follow_up", "WorkTask"),

            TopicNotifications.WorkTaskAssigneeAdded => Work("work.task.assignee_added", "assignment", "WorkTask"),
            TopicNotifications.WorkTaskDue => Work("work.task.due", "due", "WorkTask"),
            TopicNotifications.WorkPlanAssigneeAdded => Work("work.plan.assignee_added", "assignment", "WorkPlan"),

            TopicNotifications.MfgProductionOrderChangeExpectiveDate => Production("mfg.production_order.expected_date_changed", "schedule"),
            TopicNotifications.MfgProductionOrderUpdated => Production("mfg.production_order.updated", "change"),
            TopicNotifications.MfgProductionOrderDeleted => Production("mfg.production_order.deleted", "lifecycle"),
            TopicNotifications.ManufacturingFormulaAdjustmentCreated => Production("mfg.formula_adjustment.created", "formula", "ManufacturingFormula"),

            TopicNotifications.SampleRequestMessageCreated => SampleRequest("plm.sample_request.message.created", "message"),
            TopicNotifications.SampleRequestPriceQuoteRequested => SampleRequest("plm.sample_request.price_quote.requested", "quotation"),
            TopicNotifications.SampleRequestChangeRequested => SampleRequest("plm.sample_request.change.requested", "change"),
            TopicNotifications.SampleRequestUpdateRequested => SampleRequest("plm.sample_request.update.requested", "change"),
            TopicNotifications.SampleRequestUpdateApproved => SampleRequest("plm.sample_request.update.approved", "change"),
            TopicNotifications.SampleRequestUpdateRejected => SampleRequest("plm.sample_request.update.rejected", "change"),
            TopicNotifications.InternalMailMessageCreated => InternalMail("internal_mail.message.created", "message"),

            TopicNotifications.SampleRequestDataChangeRequested => SampleRequest("plm.sample_request.data_change.requested", "change"),
            TopicNotifications.SampleRequestDataChangeApproved => SampleRequest("plm.sample_request.data_change.approved", "change"),
            TopicNotifications.SampleRequestDataChangeRejected => SampleRequest("plm.sample_request.data_change.rejected", "change"),
            TopicNotifications.MerchandiseOrderDeliveryPaused => SalesOrder("sales.merchandise_order.delivery.paused", "delivery"),
            TopicNotifications.MerchandiseOrderDeliveryResumed => SalesOrder("sales.merchandise_order.delivery.resumed", "delivery"),
            TopicNotifications.QuotationSent => Quotation("crm.quotation.sent", "status"),
            TopicNotifications.QuotationMessageCreated => Quotation("crm.quotation.message.created", "message"),
            TopicNotifications.QuotationRequested => Quotation("crm.quotation.requested", "request"),
            TopicNotifications.SampleRequestSampleSent => SampleRequest("plm.sample_request.sample_sent", "sample"),
            TopicNotifications.SampleRequestFormulaCompleted => SampleRequest("plm.sample_request.formula.completed", "formula"),
            TopicNotifications.SampleRequestFormulaUpdateRequested => SampleRequest("plm.sample_request.formula_update.requested", "formula"),
            TopicNotifications.SampleRequestFormulaUpdateApproved => SampleRequest("plm.sample_request.formula_update.approved", "formula"),
            TopicNotifications.SampleRequestFormulaUpdateRejected => SampleRequest("plm.sample_request.formula_update.rejected", "formula"),
            TopicNotifications.SampleRequestFormulaUpdateCancelled => SampleRequest("plm.sample_request.formula_update.cancelled", "formula"),
            TopicNotifications.SampleRequestDirectPatchNotified => SampleRequest("plm.sample_request.direct_patch.notified", "change"),
            TopicNotifications.CustomerAiSummaryAutomationStatus => System("dev.customer.ai_summary.automation_status", "automation", "Customer"),
            TopicNotifications.ComplaintInitialDecision => SalesOrder("plm.complaint.initial_decision", "complaint", "ComplaintReport"),
            TopicNotifications.ComplaintFinalDecision => SalesOrder("plm.complaint.final_decision", "complaint", "ComplaintReport"),
            TopicNotifications.SampleRequestCustomerFeedbackRecorded => SampleRequest("plm.sample_request.customer_feedback.recorded", "sample"),
            TopicNotifications.QuotationPricingApproved => Quotation("crm.quotation.pricing.approved", "pricing"),

            _ => System($"{UnknownCodePrefix}{(int)topic}", "unknown", null)
        };
    }

    public static bool IsConfigured(TopicNotifications topic)
        => !GetDefinition(topic).Code.StartsWith(UnknownCodePrefix, StringComparison.Ordinal);

    public static IReadOnlyCollection<TopicNotifications> GetTopics(
        string? categoryCode,
        string? eventGroupCode = null)
    {
        var normalizedCategory = NormalizeCode(categoryCode);
        var normalizedEventGroup = NormalizeCode(eventGroupCode);

        return Enum.GetValues<TopicNotifications>()
            .Where(IsConfigured)
            .Where(topic => normalizedCategory is null ||
                GetDefinition(topic).CategoryCode == normalizedCategory)
            .Where(topic => normalizedEventGroup is null ||
                GetDefinition(topic).EventGroupCode == normalizedEventGroup)
            .ToArray();
    }

    public static bool IsKnownCategory(string categoryCode)
    {
        var normalizedCode = NormalizeCode(categoryCode);
        return normalizedCode == NotificationCategoryCodes.LegacyData ||
            GetDefinitions().Any(x => x.CategoryCode == normalizedCode);
    }

    public static bool IsKnownEventGroup(string eventGroupCode)
    {
        var normalizedCode = NormalizeCode(eventGroupCode);
        return GetDefinitions().Any(x => x.EventGroupCode == normalizedCode);
    }

    public static IReadOnlyList<string> GetCategoryCodes()
        => NotificationCategoryCodes.Ordered;

    public static IReadOnlyList<string> GetEventGroupCodes(string categoryCode)
    {
        var normalizedCategory = NormalizeCode(categoryCode);
        if (normalizedCategory == NotificationCategoryCodes.LegacyData)
        {
            return Array.Empty<string>();
        }

        return GetDefinitions()
            .Where(x => x.CategoryCode == normalizedCategory)
            .Select(x => x.EventGroupCode)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();
    }

    public static string? NormalizeCode(string? code)
        => string.IsNullOrWhiteSpace(code)
            ? null
            : code.Trim().ToLowerInvariant();

    private static IReadOnlyCollection<NotificationTopicDefinition> GetDefinitions()
        => Enum.GetValues<TopicNotifications>()
            .Where(IsConfigured)
            .Select(GetDefinition)
            .ToArray();

    private static NotificationTopicDefinition SampleRequest(
        string code,
        string eventGroupCode,
        string aggregateType = "SampleRequest")
        => new(code, NotificationCategoryCodes.SampleRequest, eventGroupCode, aggregateType);

    private static NotificationTopicDefinition SalesOrder(
        string code,
        string eventGroupCode,
        string aggregateType = "MerchandiseOrder")
        => new(code, NotificationCategoryCodes.SalesOrder, eventGroupCode, aggregateType);

    private static NotificationTopicDefinition Production(
        string code,
        string eventGroupCode,
        string aggregateType = "MfgProductionOrder")
        => new(code, NotificationCategoryCodes.Production, eventGroupCode, aggregateType);

    private static NotificationTopicDefinition Warehouse(string code, string eventGroupCode, string aggregateType)
        => new(code, NotificationCategoryCodes.Warehouse, eventGroupCode, aggregateType);

    private static NotificationTopicDefinition Customer(string code, string eventGroupCode, string aggregateType)
        => new(code, NotificationCategoryCodes.Customer, eventGroupCode, aggregateType);

    private static NotificationTopicDefinition Quotation(string code, string eventGroupCode)
        => new(code, NotificationCategoryCodes.Quotation, eventGroupCode, "Quotation");

    private static NotificationTopicDefinition Work(string code, string eventGroupCode, string aggregateType)
        => new(code, NotificationCategoryCodes.Work, eventGroupCode, aggregateType);

    private static NotificationTopicDefinition InternalMail(string code, string eventGroupCode)
        => new(code, NotificationCategoryCodes.InternalMail, eventGroupCode, "InternalConversation");

    private static NotificationTopicDefinition System(
        string code,
        string eventGroupCode,
        string? aggregateType)
        => new(code, NotificationCategoryCodes.System, eventGroupCode, aggregateType);
}
