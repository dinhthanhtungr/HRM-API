using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Enums.Boms;

namespace HRM.Domain.Entities.BomSchema;

/// <summary>
/// Phiên bản bất biến sau khi release của một BOM master.
/// </summary>
public class BomVersion
{
    public Guid BomVersionId { get; set; }
    public Guid BomDefinitionId { get; set; }
    public int VersionNo { get; set; }
    public BomVersionStatus Status { get; set; } = BomVersionStatus.Draft;
    public decimal BaseOutputQuantity { get; set; }
    public string OutputUnit { get; set; } = string.Empty;
    public Guid? SourceEngineeringBomVersionId { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public string? ChangeReason { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedDate { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime? ReleasedDate { get; set; }
    public Guid? ReleasedBy { get; set; }

    public virtual BomDefinition BomDefinition { get; set; } = null!;
    public virtual BomVersion? SourceEngineeringBomVersion { get; set; }
    public virtual ICollection<BomVersion> DerivedManufacturingBomVersions { get; set; } = new List<BomVersion>();
    public virtual ICollection<BomVersionItem> Items { get; set; } = new List<BomVersionItem>();
    public virtual ICollection<ManufacturingBomStage> ManufacturingStages { get; set; } = new List<ManufacturingBomStage>();
    public virtual ICollection<ManufacturingBomLossRule> LossRules { get; set; } = new List<ManufacturingBomLossRule>();
    public virtual ICollection<ProductStandardBomVersion> StandardProductAssignments { get; set; } = new List<ProductStandardBomVersion>();
    public virtual ICollection<ManufacturingFormula> ExecutionFormulas { get; set; } = new List<ManufacturingFormula>();
}
