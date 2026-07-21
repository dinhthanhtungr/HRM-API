using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.HrSchema;

namespace HRM.Domain.Entities.WorkTaskSchema;

/// <summary>
/// Danh sách/cột task cá nhân của từng employee trong module Work.
/// </summary>
public sealed class WorkTaskList
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid OwnerEmployeeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime? UpdatedDate { get; set; }
    public Guid? UpdatedBy { get; set; }

    public Company Company { get; set; } = default!;
    public Employee OwnerEmployee { get; set; } = default!;
    public Employee CreatedByNavigation { get; set; } = default!;
    public Employee? UpdatedByNavigation { get; set; }
    public ICollection<WorkTask> WorkTasks { get; set; } = new List<WorkTask>();
}
