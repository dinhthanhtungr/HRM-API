namespace HRM.Domain.Entities.BomSchema;

/// <summary>
/// Công đoạn sản xuất thuộc một phiên bản M-BOM.
/// </summary>
public class ManufacturingBomStage
{
    public Guid ManufacturingBomStageId { get; set; }
    public Guid BomVersionId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int SequenceNo { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public virtual BomVersion BomVersion { get; set; } = null!;
    public virtual ICollection<BomVersionItem> Items { get; set; } = new List<BomVersionItem>();
    public virtual ICollection<ManufacturingBomLossRule> LossRules { get; set; } = new List<ManufacturingBomLossRule>();
}
