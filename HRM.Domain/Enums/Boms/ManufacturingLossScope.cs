namespace HRM.Domain.Enums.Boms;

/// <summary>Vị trí trong quy trình M-BOM mà định mức hao hụt được áp dụng.</summary>
public enum ManufacturingLossScope
{
    Material = 1,
    Stage = 2,
    Transition = 3,
    OverallBom = 4
}
