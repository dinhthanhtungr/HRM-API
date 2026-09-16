using System.Text.Json;
using HRM.Domain.Entities.HrSchema;

namespace HRM.Domain.Entities.MaterialSchema;

/// <summary>
/// Một phương án NVL thay thế để Lab tham khảo và tự chọn khi cập nhật công thức.
/// </summary>
public sealed class MaterialReplacement
{
    public Guid MaterialReplacementId { get; set; }

    public Guid SourceMaterialId { get; set; }

    public Guid ReplacementMaterialId { get; set; }

    public JsonDocument ApplicableContext { get; set; } = JsonDocument.Parse("{}");

    public string? TechnicalNote { get; set; }

    public decimal? ReplacementRatio { get; set; }

    public int Priority { get; set; } = 1;

    public bool IsRecommended { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public Guid? UpdatedBy { get; set; }

    public Material SourceMaterial { get; set; } = null!;

    public Material ReplacementMaterial { get; set; } = null!;

    public Employee? CreatedByNavigation { get; set; }

    public Employee? UpdatedByNavigation { get; set; }
}
