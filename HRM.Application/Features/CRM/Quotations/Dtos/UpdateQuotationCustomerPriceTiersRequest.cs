namespace HRM.Application.Features.CRM.Quotations.Dtos;

public sealed class UpdateQuotationCustomerPriceTiersRequest
{
    public DateTime? ExpectedUpdatedDate { get; init; }
    /// <summary>New reason for a below-standard send request after editing status 90; stored only in its message Body.</summary>
    public string? SendConfirmationNote { get; init; }
    public IReadOnlyList<UpdateQuotationCustomerPriceTierLineRequest> Lines { get; init; } = [];
}

public sealed class UpdateQuotationCustomerPriceTierLineRequest
{
    public bool NoteOnly { get; init; }
    public Guid QuotationLineId { get; init; }
    public IReadOnlyList<QuotationLinePriceTierRequest> PriceTiers { get; init; } = [];
    public string? Note { get; init; }
}
