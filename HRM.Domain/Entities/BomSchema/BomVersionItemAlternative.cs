using System.Text.Json;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.Boms;

namespace HRM.Domain.Entities.BomSchema;

/// <summary>
/// Một đầu vào thay thế đã được phê duyệt cho đúng dòng của một BOM version.
/// Dữ liệu điều kiện và tỷ lệ là snapshot, không lấy động từ MaterialReplacement.
/// </summary>
public class BomVersionItemAlternative
{
    public Guid BomVersionItemAlternativeId { get; set; }
    public Guid BomVersionItemId { get; set; }
    public BomVersionItemAlternativeType AlternativeItemType { get; set; }
    public Guid? AlternativeMaterialId { get; set; }
    public Guid? AlternativeComponentProductId { get; set; }
    public Guid? MaterialReplacementId { get; set; } // Liên kết đến hồ sơ đề xuất/phê duyệt thay thế NVL ban đầu, giúp truy xuất nguồn gốc quyết định.
    public decimal ReplacementRatio { get; set; } = 1m;
    public int Priority { get; set; } = 1;
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
    public JsonDocument ApplicableContextSnapshot { get; set; } = JsonDocument.Parse("{}");
    public string? TechnicalNoteSnapshot { get; set; }
    public DateTime CreatedDate { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime? UpdatedDate { get; set; }
    public Guid? UpdatedBy { get; set; }

    public virtual BomVersionItem BomVersionItem { get; set; } = null!;
    public virtual Material? AlternativeMaterial { get; set; }
    public virtual Product? AlternativeComponentProduct { get; set; }
    public virtual MaterialReplacement? MaterialReplacement { get; set; }
    public virtual ICollection<MfgProductionOrderBomItemSubstitution> ProductionOrderSubstitutions { get; set; } = new List<MfgProductionOrderBomItemSubstitution>();
}
