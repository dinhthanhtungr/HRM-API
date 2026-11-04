namespace HRM.Application.Features.PLM.SampleRequests.SampleReceiptConfirmations;

public sealed class SampleReceiptActionDto
{
    public Guid SampleRequestId { get; set; }
    public Guid SampleRequestSampleTrialId { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool CanConfirm { get; set; }
    public DateTime? SampleReceivedDate { get; set; }
    public Guid? SampleReceivedByEmployeeId { get; set; }
    public string? SampleReceivedByName { get; set; }
    public DateTime? SampleReceiptConfirmedAt { get; set; }
}
