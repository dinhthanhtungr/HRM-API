using VietausWebAPI.Core.Domain.Entities.CompanySchema;
using VietausWebAPI.Core.Domain.Entities.HrSchema;

namespace VietausWebAPI.Core.Domain.Entities.DevandqaSchema;

/// <summary>Danh mục phương pháp đo theo công ty, dùng để chọn vào chỉ tiêu và template.</summary>
public class MeasurementMethod
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime? UpdatedDate { get; set; }
    public Company Company { get; set; } = null!;
    public Employee CreatedByEmployee { get; set; } = null!;
    public Employee? UpdatedByEmployee { get; set; }
}
