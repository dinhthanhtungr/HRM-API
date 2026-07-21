namespace HRM.Domain.Enums.SampleRequests;

/// <summary>
/// Loai loi nhan/yeu cau ma Sale gui trong ngu canh mot yeu cau phoi mau.
/// Day la enum nghiep vu con, khong thay the TopicNotifications cua he thong notification.
/// </summary>
public enum SampleRequestNotificationType
{
    PriceQuoteRequest = 0,
    ChangeRequest = 1,
    UpdateRequest = 2,
    GeneralMessage = 3
}
