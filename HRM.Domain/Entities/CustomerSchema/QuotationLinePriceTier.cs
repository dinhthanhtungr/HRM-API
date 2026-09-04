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
    public decimal UnitPrice { get; private set; }
    public decimal CommissionAmount { get; private set; }
    public decimal CustomerUnitPrice { get; private set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public virtual QuotationLine QuotationLine { get; set; } = null!;

    public void SetPrices(decimal unitPrice, decimal commissionAmount)
    {
        if (unitPrice < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(unitPrice));
        }

        if (commissionAmount < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(commissionAmount));
        }

        UnitPrice = unitPrice;
        CommissionAmount = commissionAmount;
        CustomerUnitPrice = unitPrice + commissionAmount;
    }
}
