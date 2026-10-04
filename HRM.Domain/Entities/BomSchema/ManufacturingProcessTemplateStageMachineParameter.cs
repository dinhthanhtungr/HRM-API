namespace HRM.Domain.Entities.BomSchema;

/// <summary>Thông số vận hành được cấu hình cho một máy tại một công đoạn trong process template.</summary>
public class ManufacturingProcessTemplateStageMachineParameter
{
    public Guid ManufacturingProcessTemplateStageMachineParameterId { get; set; }
    public Guid ManufacturingProcessTemplateStageMachineId { get; set; }
    public string ParameterCode { get; set; } = string.Empty;
    public string ParameterName { get; set; } = string.Empty;
    public decimal? TargetValue { get; set; }
    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }
    public string Unit { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public int SequenceNo { get; set; }
    public string? Note { get; set; }

    public virtual ManufacturingProcessTemplateStageMachine StageMachine { get; set; } = null!;
}
