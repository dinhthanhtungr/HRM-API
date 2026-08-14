namespace HRM.Domain.Entities.SampleRequestSchema;

/// <summary>
/// Snapshot nghiệp vụ bất biến của một công thức phát triển.
/// </summary>
public class FormulaVersion
{
    public Guid FormulaVersionId { get; set; } = Guid.CreateVersion7();
    public Guid FormulaId { get; set; }
    public int VersionNo { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";
    public string? Note { get; set; }

    public decimal TotalPrice { get; set; }
    public decimal? ProductionPrice { get; set; }
    public decimal? PresidentPrice { get; set; }
    public decimal? ProfitMarginPrice { get; set; }

    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }

    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public string? ChangeReason { get; set; }

    public virtual Formula Formula { get; set; } = null!;
    public virtual ICollection<FormulaVersionItem> Items { get; set; } = [];
}
