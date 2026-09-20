using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Enums.Boms;

namespace HRM.Domain.Entities.BomSchema;

/// <summary>
/// Bộ định mức hao hụt tái sử dụng theo công ty. Khi áp dụng vào M-BOM Draft,
/// các rule được sao chép thành snapshot của version BOM.
/// </summary>
public class ManufacturingLossProfile
{
    public Guid ManufacturingLossProfileId { get; set; }
    public Guid CompanyId { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ManufacturingLossProfileStatus Status { get; set; } = ManufacturingLossProfileStatus.Draft;
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedDate { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime? ReleasedDate { get; set; }
    public Guid? ReleasedBy { get; set; }
    public DateTime? UpdatedDate { get; set; }
    public Guid? UpdatedBy { get; set; }

    public virtual Company Company { get; set; } = null!;
    public virtual ICollection<ManufacturingLossProfileRule> Rules { get; set; } = new List<ManufacturingLossProfileRule>();
}
