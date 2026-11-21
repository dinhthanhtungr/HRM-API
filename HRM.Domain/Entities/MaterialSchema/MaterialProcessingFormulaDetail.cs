using HRM.Domain.Enums.Formulas;
namespace HRM.Domain.Entities.MaterialSchema;

public sealed class MaterialProcessingFormulaDetail
{
    public Guid MaterialProcessingFormulaDetailId { get; set; }
    public Guid MaterialProcessingFormulaId { get; set; }
    public Guid MaterialId { get; set; }
    public int LineNo { get; set; }
    public decimal QuantityKg { get; set; }
    public string? LotNo { get; set; }
    public ItemType itemType { get; set; } = ItemType.Material;
    public string MaterialCodeSnapshot { get; set; } = string.Empty;
    public string MaterialNameSnapshot { get; set; } = string.Empty;
    public MaterialProcessingFormula Formula { get; set; } = null!;
    public Material Material { get; set; } = null!;
}
