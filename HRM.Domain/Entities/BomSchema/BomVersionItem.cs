using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.Formulas;

namespace HRM.Domain.Entities.BomSchema;

/// <summary>
/// Một dòng nguyên liệu hoặc bán thành phẩm trong phiên bản BOM.
/// </summary>
public class BomVersionItem
{
    public Guid BomVersionItemId { get; set; }
    public Guid BomVersionId { get; set; }
    public int LineNo { get; set; }
    public ItemType ItemType { get; set; }
    public Guid? MaterialId { get; set; }
    public Guid? ComponentProductId { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? ManufacturingBomStageId { get; set; }
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string? MaterialExternalIdSnapshot { get; set; }
    public string? MaterialNameSnapshot { get; set; }
    public string? Note { get; set; }

    public virtual BomVersion BomVersion { get; set; } = null!;
    public virtual Material? Material { get; set; }
    public virtual Product? ComponentProduct { get; set; }
    public virtual Category? Category { get; set; }
    public virtual ManufacturingBomStage? ManufacturingStage { get; set; }
    public virtual ICollection<ManufacturingBomLossRule> LossRules { get; set; } = new List<ManufacturingBomLossRule>();
}
