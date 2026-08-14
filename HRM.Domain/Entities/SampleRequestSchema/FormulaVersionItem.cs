using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Enums.Formulas;

namespace HRM.Domain.Entities.SampleRequestSchema;

/// <summary>
/// Snapshot bất biến của một dòng nguyên liệu hoặc sản phẩm trong FormulaVersion.
/// </summary>
public class FormulaVersionItem
{
    public Guid FormulaVersionItemId { get; set; } = Guid.CreateVersion7();
    public Guid FormulaVersionId { get; set; }

    public int LineNo { get; set; }
    public ItemType ItemType { get; set; }
    public Guid? MaterialId { get; set; }
    public Guid? ProductId { get; set; }
    public Guid CategoryId { get; set; }

    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }

    public string? Unit { get; set; }
    public string? MaterialExternalIdSnapshot { get; set; }
    public string? MaterialNameSnapshot { get; set; }

    public virtual FormulaVersion Version { get; set; } = null!;
    public virtual Material? Material { get; set; }
    public virtual Product? Product { get; set; }
    public virtual Category Category { get; set; } = null!;
}
