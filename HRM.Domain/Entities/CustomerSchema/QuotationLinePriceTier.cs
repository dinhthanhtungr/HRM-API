namespace HRM.Domain.Entities.CustomerSchema;

public class QuotationLinePriceTier
{
    public Guid QuotationLinePriceTierId { get; set; }
    public Guid QuotationLineId { get; set; }

    public string QuantityRangeLabel { get; set; } = string.Empty;
    public decimal? MinQuantity { get; set; }
    public decimal? MaxQuantity { get; set; }
    public bool MinInclusive { get; set; } = true;
    public bool MaxInclusive { get; set; } = true;
    public decimal UnitPrice { get; set; }
    public int SortOrder { get; set; }

    public virtual QuotationLine QuotationLine { get; set; } = null!;
}
