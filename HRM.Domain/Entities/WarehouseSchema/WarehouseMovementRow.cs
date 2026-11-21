namespace HRM.Domain.Entities.WarehouseSchema;

/// <summary>Read-only projection of the existing canonical stock movement view.</summary>
public sealed class WarehouseMovementRow
{
    public Guid CompanyId { get; set; }
    public string MovementKey { get; set; } = string.Empty;
    public Guid? MovementId { get; set; }
    public bool IsCanonicalMovement { get; set; }
    public string MovementType { get; set; } = string.Empty;
    public DateTime MovementAt { get; set; }
    public long LedgerId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Scope { get; set; } = string.Empty;
    public int SlotId { get; set; }
    public string ShelfStockCode { get; set; } = string.Empty;
    public int? ShelfStockId { get; set; }
    public string? ProductCode { get; set; }
    public string? LotNumber { get; set; }
    public int? StockType { get; set; }
    public string? UnitName { get; set; }
    public decimal DeltaKg { get; set; }
    public decimal BeforeKg { get; set; }
    public decimal AfterKg { get; set; }
    public Guid? CreatedBy { get; set; }
    public string? RequestCode { get; set; }
    public string? AppSource { get; set; }
    public long? VoucherId { get; set; }
    public long? VoucherDetailId { get; set; }
}
