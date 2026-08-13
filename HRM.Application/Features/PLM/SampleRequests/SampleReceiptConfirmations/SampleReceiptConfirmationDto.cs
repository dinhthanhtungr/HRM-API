namespace HRM.Application.Features.PLM.SampleRequests.SampleReceiptConfirmations;

public sealed class SampleReceiptConfirmationDto
{
    public Guid SampleRequestId { get; set; }
    public Guid SampleRequestSampleTrialId { get; set; }
    public Guid MessageId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime SampleReceivedDate { get; set; }
    public Guid SampleReceivedByEmployeeId { get; set; }
    public string? SampleReceivedByName { get; set; }
    public DateTime SampleReceiptConfirmedAt { get; set; }
    public DateTime UpdatedDate { get; set; }
}
