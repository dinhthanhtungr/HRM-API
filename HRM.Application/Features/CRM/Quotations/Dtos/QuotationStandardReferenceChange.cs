using HRM.Application.Commons.Models;

namespace HRM.Application.Features.CRM.Quotations.Dtos;

public sealed record QuotationStandardReferenceTier(string Label, decimal? MinQuantity, decimal? MaxQuantity,
    bool MinInclusive, bool MaxInclusive, decimal UnitPrice, int SortOrder, bool IsActive);

public sealed record QuotationStandardReferenceChange(Guid QuotationLineId, Guid ProductId, string ProductCode,
    Guid PreviousVersionId, Guid? CurrentVersionId, decimal? PreviousPrice, decimal? CurrentPrice,
    string PreviousCurrency, string? CurrentCurrency, IReadOnlyList<QuotationStandardReferenceTier> PreviousTiers,
    IReadOnlyList<QuotationStandardReferenceTier> CurrentTiers, string? AcceptanceToken, bool IsUnavailable, bool RequiresAcceptance);

public sealed class QuotationStandardReferenceFailure(IReadOnlyList<QuotationStandardReferenceChange> changes)
    : OperationResult<QuotationTotalsDto>
{
    public string Code => "quotation.standard_price_changed";
    public IReadOnlyList<QuotationStandardReferenceChange> Changes { get; } = changes;
}
