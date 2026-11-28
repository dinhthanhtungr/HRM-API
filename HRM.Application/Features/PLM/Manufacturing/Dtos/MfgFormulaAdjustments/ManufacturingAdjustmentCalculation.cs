namespace HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulaAdjustments;

/// <summary>Calculated draft only. No IDs in Adjustment identify stored records.</summary>
public sealed class ManufacturingAdjustmentCalculation
{
    public bool IsStored => false;
    public GetManufacturingFormulaAdjustment Adjustment { get; init; } = new();
    public GetManufacturingFormulaAdjustmentBatchGroups Groups { get; init; } = new();
    public IReadOnlyList<ManufacturingAdjustmentTrialCalculation> Trials { get; init; } = [];
}

public sealed class ManufacturingAdjustmentTrialCalculation
{
    public int BatchNo { get; init; }
    public int TrialNo { get; init; }
    public decimal OriginalBatchQuantityGram { get; init; }
    public decimal NewWeighingTotalGram { get; init; }
    public decimal ColourDeltaTotalGram { get; init; }
    public decimal FinalQuantityGram { get; init; }
    public IReadOnlyList<ManufacturingAdjustmentItemCalculation> Items { get; init; } = [];
}

public sealed class ManufacturingAdjustmentItemCalculation
{
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
    public HRM.Domain.Enums.Formulas.ItemType ItemType { get; init; }
    public Guid ItemId { get; init; }
    public bool IsMaterial { get; init; }
    public decimal OriginalBaseQuantityGram { get; init; }
    public decimal NewWeighingTotalGram { get; init; }
    public decimal ColourDeltaTotalGram { get; init; }
    public decimal ActualQuantityGram { get; init; }
    public decimal DeltaFromOriginalGram { get; init; }
}
