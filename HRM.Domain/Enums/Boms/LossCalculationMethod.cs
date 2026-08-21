namespace HRM.Domain.Enums.Boms;

public enum LossCalculationMethod
{
    PercentOfMaterial = 1,
    PercentOfStageInput = 2,
    PercentOfStageOutput = 3,
    FixedPerRun = 4,
    FixedPerBatch = 5,
    FixedPerEvent = 6,
    ActualOnly = 7
}
