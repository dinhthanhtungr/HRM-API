namespace HRM.Application.Features.CRM.Quotations.Dtos;

/// <summary>One atomic update; null sections are unchanged, empty lists explicitly replace their contents.</summary>
public sealed class SaveQuotationRequest
{
    public DateTime? ExpectedUpdatedDate { get; init; }
    public UpdateQuotationRequest? Header { get; init; }
    public IReadOnlyList<string> ClearFields { get; init; } = [];
    public IReadOnlyList<QuotationLineRequest>? Lines { get; init; }
    public IReadOnlyList<UpdateQuotationCustomerPriceTierLineRequest>? CustomerPriceLines { get; init; }
}
