namespace HRM.Application.Features.CRM.Quotations.Dtos;

using HRM.Domain.Enums.CustomerEnum;

public sealed class QuotationPricingApprovedNotificationPayload
{
    public string ContentType { get; init; } = "QuotationPricingApproved";
    public Guid QuotationId { get; init; }
    public string QuotationExternalId { get; init; } = string.Empty;
    public Guid ProductId { get; init; }
    public string ProductCode { get; init; } = string.Empty;
    public Guid ProductPricingVersionId { get; init; }
    public int ProductPricingVersion { get; init; }
    public QuotationStatus QuotationStatus { get; init; }
    public bool IsQuotationPricingComplete { get; init; }
    public QuotationPricingApprovedActionDto Action { get; init; } = new();
}

public sealed class QuotationPricingApprovedActionDto
{
    public string Code { get; init; } = "Quotation.Open";
    public QuotationPricingApprovedActionParametersDto Parameters { get; init; } = new();
}

public sealed class QuotationPricingApprovedActionParametersDto
{
    public Guid QuotationId { get; init; }
    public string QuotationExternalId { get; init; } = string.Empty;
}
