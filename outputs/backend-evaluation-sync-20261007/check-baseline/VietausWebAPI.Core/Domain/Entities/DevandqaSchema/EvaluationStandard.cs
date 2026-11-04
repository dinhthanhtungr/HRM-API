using VietausWebAPI.Core.Domain.Entities.CompanySchema;
using VietausWebAPI.Core.Domain.Entities.HrSchema;

namespace VietausWebAPI.Core.Domain.Entities.DevandqaSchema;

/// <summary>Danh mục tiêu chuẩn đánh giá IQC có thể quản lý theo công ty.</summary>
public class EvaluationStandard
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid CompanyId { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedDate { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime? UpdatedDate { get; set; }
    public Guid? UpdatedBy { get; set; }

    public virtual Company Company { get; set; } = null!;
    public virtual Employee CreatedByEmployee { get; set; } = null!;
    public virtual Employee? UpdatedByEmployee { get; set; }
}
