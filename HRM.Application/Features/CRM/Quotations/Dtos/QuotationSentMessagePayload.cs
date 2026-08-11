namespace HRM.Application.Features.CRM.Quotations.Dtos;

public sealed class QuotationSentMessagePayload
{
    public string ContentType { get; init; } = "QuotationSent";
    public Guid ConversationId { get; init; }
    public Guid MessageId { get; init; }
    public Guid QuotationId { get; init; }
    public string QuotationExternalId { get; init; } = string.Empty;
    public Guid CustomerInteractionId { get; init; }
    public Guid CustomerId { get; init; }
}
