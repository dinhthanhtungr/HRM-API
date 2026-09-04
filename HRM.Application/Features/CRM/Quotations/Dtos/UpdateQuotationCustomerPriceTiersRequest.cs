namespace HRM.Application.Features.CRM.Quotations.Dtos;

public sealed class UpdateQuotationCustomerPriceTiersRequest
{
    public DateTime? ExpectedUpdatedDate { get; init; }
    public IReadOnlyList<UpdateQuotationCustomerPriceTierLineRequest> Lines { get; init; } = [];
}

public sealed class UpdateQuotationCustomerPriceTierLineRequest
{
    public Guid QuotationLineId { get; init; }
    public IReadOnlyList<QuotationLinePriceTierRequest> PriceTiers { get; init; } = [];
    public string? Note { get; init; }
}
