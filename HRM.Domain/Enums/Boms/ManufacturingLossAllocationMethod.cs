namespace HRM.Domain.Enums.Boms;

/// <summary>Cách xử lý hao hụt chung khi tính nhu cầu nguyên vật liệu.</summary>
public enum ManufacturingLossAllocationMethod
{
    DirectMaterial = 1,
    ProportionalToBomItems = 2,
    None = 3
}
