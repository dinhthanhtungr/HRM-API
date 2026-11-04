namespace HRM.Application.Features.CRM.Quotations.Dtos;

/// <summary>Stored only on the existing internal message; the reason lives in its Body.</summary>
public sealed class QuotationSendConfirmationPayload
{
    public const string ContentTypeCode = "QuotationSendConfirmation";
    public const string PendingStatus = "pending";
    public const string ConfirmedStatus = "confirmed";
    public const string SupersededStatus = "superseded";
    public const int CurrentComparisonRuleVersion = 3;
    public int ComparisonRuleVersion { get; set; }
    public DateTime? PricingEvaluatedAt { get; set; }
    public int? CommercialFingerprintVersion { get; set; }
    public string? CommercialFingerprint { get; set; }
    public string ContentType { get; set; } = ContentTypeCode;
    public string ActionType { get; set; } = "quotation.confirm-sent";
    public Guid QuotationId { get; set; }
    public Guid RequestMessageId { get; set; }
    public Guid ConversationId { get; set; }
    public string Status { get; set; } = PendingStatus;
    public bool IsUrgent { get; set; } = true;
    public DateTime? ExpectedUpdatedDate { get; set; }
    public DateTime? SupersededAt { get; set; }
    public Guid? SupersededBy { get; set; }
    public Guid? ConfirmedBy { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public IReadOnlyList<QuotationSendPriceSegment> Comparison { get; set; } = [];
}

public sealed class ConfirmQuotationSentRequest
{
    public Guid? RequestMessageId { get; init; }
    public DateTime? ExpectedUpdatedDate { get; init; }
}
