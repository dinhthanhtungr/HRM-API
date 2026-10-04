namespace HRM.Domain.Entities.BomSchema;

/// <summary>Snapshot thông số vận hành của một máy trong đúng một M-BOM version.</summary>
public class ManufacturingBomStageMachineParameter
{
    public Guid ManufacturingBomStageMachineParameterId { get; set; }
    public Guid ManufacturingBomStageMachineId { get; set; }
    public string ParameterCodeSnapshot { get; set; } = string.Empty;
    public string ParameterNameSnapshot { get; set; } = string.Empty;
    public decimal? TargetValueSnapshot { get; set; }
    public decimal? MinValueSnapshot { get; set; }
    public decimal? MaxValueSnapshot { get; set; }
    public string UnitSnapshot { get; set; } = string.Empty;
    public bool IsRequiredSnapshot { get; set; }
    public int SequenceNo { get; set; }
    public string? NoteSnapshot { get; set; }

    public virtual ManufacturingBomStageMachine StageMachine { get; set; } = null!;
}
