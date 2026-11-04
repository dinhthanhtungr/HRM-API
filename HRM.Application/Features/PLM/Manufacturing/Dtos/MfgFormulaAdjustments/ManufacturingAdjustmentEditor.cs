namespace HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulaAdjustments;

/// <summary>Server-owned editor initialization. Draft is a full replacement, not an append request.</summary>
public sealed class ManufacturingAdjustmentEditor
{
    public Guid? SourceAdjustmentId { get; init; }
    public int PlannedBatchCount { get; init; }
    public decimal PlannedBatchQuantityGram { get; init; }
    public ManufacturingAdjustmentInput Draft { get; init; } = new();
    public ManufacturingAdjustmentCalculation Calculation { get; init; } = new();
}
