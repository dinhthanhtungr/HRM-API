namespace HRM.Application.Features.CRM.Quotations.Dtos;

/// <summary>One atomic update; null sections are unchanged, empty lists explicitly replace their contents.</summary>
public sealed class SaveQuotationRequest
{
    public bool SyncStandardReferences { get; init; }
    public IReadOnlyDictionary<Guid, string> AcceptedStandardPriceChanges { get; init; } = new Dictionary<Guid, string>();
    public IReadOnlyDictionary<Guid, Guid> ObservedStandardPriceVersions { get; init; } = new Dictionary<Guid, Guid>();
    public DateTime? ExpectedUpdatedDate { get; init; }
    /// <summary>New reason for a below-standard send request after editing status 90; stored only in its message Body.</summary>
    public string? SendConfirmationNote { get; init; }
    public UpdateQuotationRequest? Header { get; init; }
    public IReadOnlyList<string> ClearFields { get; init; } = [];
    public IReadOnlyList<QuotationLineRequest>? Lines { get; init; }
    public IReadOnlyList<UpdateQuotationCustomerPriceTierLineRequest>? CustomerPriceLines { get; init; }
}
