namespace HRM.Domain.Enums.Manufacturings;

/// <summary>Vòng đời của một đề nghị thay thế đầu vào trên lệnh sản xuất.</summary>
public enum MfgProductionOrderBomItemSubstitutionStatus
{
    Pending = 1,
    Approved = 2,
    Applied = 3,
    Rejected = 4,
    Cancelled = 5
}
