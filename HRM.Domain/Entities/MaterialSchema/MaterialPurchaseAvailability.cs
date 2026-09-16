using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Enums.Materials;

namespace HRM.Domain.Entities.MaterialSchema;

/// <summary>
/// Trạng thái mua hiện tại do bộ phận Kế hoạch cập nhật cho một nguyên vật liệu.
/// </summary>
public sealed class MaterialPurchaseAvailability
{
    public Guid MaterialPurchaseAvailabilityId { get; set; }

    public Guid MaterialId { get; set; }

    public MaterialPurchaseStatus Status { get; set; } = MaterialPurchaseStatus.Available;

    public string? Reason { get; set; }

    public DateTime? EffectiveFrom { get; set; }

    public DateTime? ExpectedAvailableDate { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedDate { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public Guid? UpdatedBy { get; set; }

    public Material Material { get; set; } = null!;

    public Employee? CreatedByNavigation { get; set; }

    public Employee? UpdatedByNavigation { get; set; }
}
