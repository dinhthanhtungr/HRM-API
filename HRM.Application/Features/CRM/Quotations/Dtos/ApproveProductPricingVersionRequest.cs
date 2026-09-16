namespace HRM.Application.Features.CRM.Quotations.Dtos;

public sealed class ApproveProductPricingVersionRequest
{
    public DateTime? ExpectedUpdatedDate { get; init; }
    public string? PublisherNote { get; init; }
}
