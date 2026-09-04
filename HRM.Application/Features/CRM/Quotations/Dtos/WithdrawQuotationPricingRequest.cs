using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Features.CRM.Quotations.Dtos;

public sealed class WithdrawQuotationPricingRequest
{
    public DateTime? ExpectedUpdatedDate { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public sealed class QuotationStatusTransitionDto
{
    public Guid QuotationId { get; init; }
    public QuotationStatus Status { get; init; }
    public DateTime? UpdatedDate { get; init; }
}
