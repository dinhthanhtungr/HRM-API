namespace HRM.Application.Features.CRM.Quotations.Dtos;

using HRM.Domain.Enums.CustomerEnum;

/// <summary>
/// Metadata của cảnh báo giá chuẩn quá hạn trong thread báo giá và Notification Hub.
/// </summary>
public sealed class QuotationPricingExpiredMessagePayload
{
    public string ContentType { get; init; } = "QuotationPricingExpired";
    public string RelatedType { get; init; } = "Quotation";
    public Guid RelatedId { get; init; }
    public string RelatedExternalId { get; init; } = string.Empty;
    public Guid ConversationId { get; init; }
    public Guid MessageId { get; init; }
    public Guid ProductId { get; init; }
    public string ProductCode { get; init; } = string.Empty;
    public Guid ProductPricingVersionId { get; init; }
    public int ProductPricingVersion { get; init; }
    public DateTime PricingReviewDueDate { get; init; }
    public QuotationStatus QuotationStatus { get; init; } = QuotationStatus.PendingApproval;
    public QuotationPricingExpiredMessageActionDto Action { get; init; } = new();
}

public sealed class QuotationPricingExpiredMessageActionDto
{
    public string Code { get; init; } = "Quotation.OpenPricingWorkspace";
    public QuotationPricingExpiredMessageActionParametersDto Parameters { get; init; } = new();
}

public sealed class QuotationPricingExpiredMessageActionParametersDto
{
    public Guid QuotationId { get; init; }
    public string QuotationExternalId { get; init; } = string.Empty;
    public Guid ProductId { get; init; }
    public Guid ProductPricingVersionId { get; init; }
}
