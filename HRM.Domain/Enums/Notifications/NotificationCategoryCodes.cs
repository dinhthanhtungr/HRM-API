namespace HRM.Domain.Enums.Notifications;

/// <summary>
/// Stable business categories used by the Notification Hub public contract.
/// </summary>
public static class NotificationCategoryCodes
{
    public const string SampleRequest = "sample_request";
    public const string Quotation = "quotation";
    public const string SalesOrder = "sales_order";
    public const string Production = "production";
    public const string Warehouse = "warehouse";
    public const string Customer = "customer";
    public const string Work = "work";
    public const string InternalMail = "internal_mail";
    public const string System = "system";
    public const string LegacyData = "legacy_data";

    public static readonly IReadOnlyList<string> Ordered =
    [
        SampleRequest,
        Quotation,
        SalesOrder,
        Production,
        Warehouse,
        Customer,
        Work,
        InternalMail,
        System,
        LegacyData
    ];
}
