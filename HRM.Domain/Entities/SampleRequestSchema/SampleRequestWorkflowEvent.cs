namespace HRM.Domain.Entities.SampleRequestSchema;

/// <summary>
/// Sự kiện nghiệp vụ bất biến của một yêu cầu mẫu; không thay thế dữ liệu Trial hoặc audit sửa field.
/// </summary>
public sealed class SampleRequestWorkflowEvent
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid CompanyId { get; set; }
    public Guid SampleRequestId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public DateTime? OccurredAt { get; set; }
    public DateTime RecordedAt { get; set; }
    public Guid? RecordedByEmployeeId { get; set; }
    public string? Note { get; set; }
    public Guid? FormulaId { get; set; }
    public Guid? SampleRequestSampleTrialId { get; set; }
    public Guid? ProductPricingVersionId { get; set; }
    public Guid? QuotationLineId { get; set; }
    public string? PriceReference { get; set; }
    public Guid? RelatedEventId { get; set; }
    public Guid? ConversationId { get; set; }
    public Guid? MessageId { get; set; }
    public Guid? NotificationId { get; set; }
}
