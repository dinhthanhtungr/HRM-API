namespace HRM.Application.Features.CRM.Quotations.Dtos;

public sealed class RequestQuotationRequest
{
    public string Message { get; init; } = string.Empty;
    public bool IsUrgent { get; init; }
}

public sealed class RequestQuotationResultDto
{
    public Guid QuotationId { get; init; }
    public Guid ConversationId { get; init; }
    public Guid MessageId { get; init; }
    public Guid NotificationId { get; init; }
    public DateTime RequestedAt { get; init; }
}
