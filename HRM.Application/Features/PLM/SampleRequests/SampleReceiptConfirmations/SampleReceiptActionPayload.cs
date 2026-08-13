namespace HRM.Application.Features.PLM.SampleRequests.SampleReceiptConfirmations;

internal static class SampleReceiptActionStatuses
{
    public const string Pending = "Pending";
    public const string Confirmed = "Confirmed";
}

/// <summary>
/// Metadata để Notification Hub hiển thị hành động Sale xác nhận đã nhận mẫu.
/// Trial vẫn là nguồn dữ liệu chính; payload chỉ giúp FE render đúng message/action.
/// </summary>
internal sealed class SampleReceiptActionPayload
{
    public Guid SampleRequestSampleTrialId { get; set; }
    public string Status { get; set; } = SampleReceiptActionStatuses.Pending;
    public DateTime? SampleReceivedDate { get; set; }
    public Guid? SampleReceivedByEmployeeId { get; set; }
    public string? SampleReceivedByName { get; set; }
    public DateTime? SampleReceiptConfirmedAt { get; set; }
}
