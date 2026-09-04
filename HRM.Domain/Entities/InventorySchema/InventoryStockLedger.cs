namespace HRM.Domain.Entities.InventorySchema;

public sealed class InventoryStockLedger
{
    public Guid LedgerId { get; set; }
    public Guid CompanyId { get; set; }
    public DateTime TxnDate { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public decimal? AvgCostAfter { get; set; }
}
