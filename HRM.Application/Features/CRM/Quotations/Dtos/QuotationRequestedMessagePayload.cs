namespace HRM.Application.Features.CRM.Quotations.Dtos;

public sealed class QuotationRequestedMessagePayload
{
    public string ContentType { get; init; } = "QuotationRequested";
    public string RelatedType { get; init; } = "Quotation";
    public Guid RelatedId { get; init; }
    public string RelatedExternalId { get; init; } = string.Empty;
    public Guid ConversationId { get; init; }
    public Guid MessageId { get; init; }
    public bool IsUrgent { get; init; }
    public QuotationRequestedMessageActionDto Action { get; init; } = new();
}

public sealed class QuotationRequestedMessageActionDto
{
    public string Code { get; init; } = "Quotation.OpenPricingOptions";
    public QuotationRequestedMessageActionParametersDto Parameters { get; init; } = new();
}

public sealed class QuotationRequestedMessageActionParametersDto
{
    public Guid QuotationId { get; init; }
    public string QuotationExternalId { get; init; } = string.Empty;
}
