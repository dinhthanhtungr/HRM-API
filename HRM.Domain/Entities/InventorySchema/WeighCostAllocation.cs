namespace HRM.Domain.Entities.InventorySchema;

public sealed class WeighCostAllocation
{
    public Guid AllocId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid EventId { get; set; }
    public decimal AmountCost { get; set; }
    public string CostStatus { get; set; } = string.Empty;
}
