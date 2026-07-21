namespace HRM.Domain.Enums.Notifications;

/// <summary>
/// Nhom nghiep vu lon de FE loc notification. TopicNotifications van mo ta su kien cu the.
/// All chi la gia tri truy van, khong duoc luu vao bang Notification.
/// </summary>
public enum NotificationCategory
{
    All = 0,
    SampleRequest = 1,
    Quotation = 2,
    Manufacturing = 3,
    MerchandiseOrder = 4,
    Delivery = 5,
    Customer = 6,
    Work = 7,
    InternalMail = 8,
    System = 9,
    LegacyData = 10
}
