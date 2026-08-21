using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Enums.Boms;

namespace HRM.Domain.Entities.BomSchema;

/// <summary>
/// Danh mục loại hao hụt mở rộng theo công ty.
/// </summary>
public class ManufacturingLossType
{
    public Guid ManufacturingLossTypeId { get; set; }
    public Guid CompanyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public LossCalculationMethod DefaultCalculationMethod { get; set; }
    public bool IsRecoverable { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime? UpdatedDate { get; set; }
    public Guid? UpdatedBy { get; set; }

    public virtual Company Company { get; set; } = null!;
    public virtual ICollection<ManufacturingBomLossRule> LossRules { get; set; } = new List<ManufacturingBomLossRule>();
}
